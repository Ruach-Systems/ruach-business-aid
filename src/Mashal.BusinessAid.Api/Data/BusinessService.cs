using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Api.Data;

public sealed class BusinessService(SqlConnectionFactory connections, IdentityRepository identities) : IBusinessService
{
    public static async Task Lock(SqlConnection c, SqlTransaction tx, Guid business)
    {
        var result = await c.ExecuteScalarAsync<int>("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;", new { key = "business:" + business }, tx);
        if (result < 0) throw new DomainException("retry", "Business is busy. Try again shortly.", 503);
    }
    public static async Task Authorize(SqlConnection c, SqlTransaction? tx, Guid user, Guid business, bool management = false)
    {
        var phone = await c.ExecuteScalarAsync<string?>("SELECT PhoneNumber FROM dbo.Users WITH(HOLDLOCK) WHERE Id=@user", new { user }, tx);
        if (phone is null) throw new DomainException("phone_required", "Add a unique Philippine mobile number before entering a business.", 403);
        var role = await c.QuerySingleOrDefaultAsync<string>("SELECT Role FROM dbo.BusinessMembers WHERE BusinessId=@business AND UserId=@user", new { business, user }, tx);
        if (role is null || management && role == "Staff") throw new DomainException("forbidden", "You do not have permission for this business.", 403);
    }
    public async Task<BootstrapResult> Bootstrap(Guid user, Guid? businessId = null)
    {
        var account = await identities.Find(user) ?? throw new DomainException("unauthorized", "Sign in again.", 401);
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        var business = businessId;
        if (business is null) return new BootstrapResult(DataModel.CurrentVersion, account, new AppData(), 0);
        await Lock(c, tx, business.Value);
        await Authorize(c, tx, user, business.Value);
        var data = await new EntityRepository(c, tx, business.Value).Load();
        var cursor = await c.ExecuteScalarAsync<long>("SELECT ISNULL(MAX([Cursor]),0) FROM dbo.ItemSyncChanges WHERE BusinessId=@business", new { business }, tx);
        await tx.CommitAsync(); return new BootstrapResult(DataModel.CurrentVersion, account, data, cursor);
    }
    public async Task Push(Guid user, PushRequest request)
    {
        if (request.ModelVersion != DataModel.CurrentVersion)
            throw new DomainException("client_upgrade_required", "Refresh Business Aid to use the simplified Items update.", 426);
        Rules.Require(request.BusinessId != Guid.Empty && request.Operations is { Count: > 0 and <= 50 }, "Submit 1–50 operations for a business.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        await Lock(c, tx, request.BusinessId);
        await Authorize(c, tx, user, request.BusinessId);
        var repository = new EntityRepository(c, tx, request.BusinessId); var handler = new CommandProcessor(repository);
        foreach (var op in request.Operations!)
        {
            Rules.Require(op.Id != Guid.Empty && op.EntityId != Guid.Empty, "Operation identifiers are required.");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(op, Wire.Json))));
            var previous = await c.QuerySingleOrDefaultAsync<string>("SELECT PayloadHash FROM dbo.ItemProcessedOperations WHERE BusinessId=@BusinessId AND Id=@Id", new { request.BusinessId, op.Id }, tx);
            if (previous is not null)
            {
                await Authorize(c, tx, user, request.BusinessId);
                if (previous != hash) throw new DomainException("conflict", "An operation ID was reused with different content.", 409);
                continue;
            }
            if (op.Type == "createBusiness")
            {
                throw new DomainException("approval_required", "Request a business from your account dashboard. RUACH Admin approval is required.", 403);
            }
            else
            {
                await Authorize(c, tx, user, request.BusinessId, management: op.Type is "saveItem" or "adjustStock" or "deleteExpense");
                await handler.Execute(op);
            }
            await c.ExecuteAsync("INSERT dbo.ItemProcessedOperations(BusinessId,Id,UserId,PayloadHash) VALUES(@BusinessId,@Id,@user,@hash)", new { request.BusinessId, op.Id, user, hash }, tx);
        }
        await tx.CommitAsync();
    }
    public async Task<PullResult> Pull(Guid user, Guid business, long cursor)
    {
        Rules.Require(cursor >= 0, "Invalid sync cursor.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        await Lock(c, tx, business); await Authorize(c, tx, user, business);
        var rows = (await c.QueryAsync<ChangeRow>("SELECT TOP(501) [Cursor],CollectionName,EntityId,Payload FROM dbo.ItemSyncChanges WHERE BusinessId=@business AND [Cursor]>@cursor ORDER BY [Cursor]", new { business, cursor }, tx)).ToList();
        var result = rows.Take(500).Select(r =>
        {
            using var document = JsonDocument.Parse(r.Payload);
            return new Change(r.Cursor, r.CollectionName, r.EntityId, document.RootElement.Clone());
        }).ToList();
        await tx.CommitAsync();
        return new PullResult(DataModel.CurrentVersion, result.LastOrDefault()?.Cursor ?? cursor, result, rows.Count > 500);
    }
    private sealed record ChangeRow(long Cursor, string CollectionName, Guid EntityId, string Payload);
}
