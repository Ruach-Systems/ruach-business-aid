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
    public static async Task Authorize(SqlConnection c, SqlTransaction? tx, Guid user, Guid business, bool write = false, bool management = false)
    {
        var role = await c.QuerySingleOrDefaultAsync<string>("SELECT Role FROM dbo.BusinessMembers WHERE BusinessId=@business AND UserId=@user", new { business, user }, tx);
        if (role is null || management && role == "Staff") throw new DomainException("forbidden", "You do not have permission for this business.", 403);
    }
    public async Task<BootstrapResult> Bootstrap(Guid user)
    {
        var account = await identities.Find(user) ?? throw new DomainException("unauthorized", "Sign in again.", 401);
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        var business = await c.QuerySingleOrDefaultAsync<Guid?>("SELECT TOP(1) BusinessId FROM dbo.BusinessMembers WHERE UserId=@user ORDER BY BusinessId", new { user }, tx);
        if (business is null) return new BootstrapResult(account, new AppData(), 0);
        await Lock(c, tx, business.Value);
        var data = await new EntityRepository(c, tx, business.Value).Load();
        var cursor = await c.ExecuteScalarAsync<long>("SELECT ISNULL(MAX([Cursor]),0) FROM dbo.SyncChanges WHERE BusinessId=@business", new { business }, tx);
        await tx.CommitAsync(); return new BootstrapResult(account, data, cursor);
    }
    public async Task Push(Guid user, PushRequest request)
    {
        Rules.Require(request.BusinessId != Guid.Empty && request.Operations is { Count: > 0 and <= 50 }, "Submit 1–50 operations for a business.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        await Lock(c, tx, request.BusinessId);
        var repository = new EntityRepository(c, tx, request.BusinessId); var handler = new CommandProcessor(repository);
        foreach (var op in request.Operations!)
        {
            Rules.Require(op.Id != Guid.Empty && op.EntityId != Guid.Empty, "Operation identifiers are required.");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(op, Wire.Json))));
            var previous = await c.QuerySingleOrDefaultAsync<string>("SELECT PayloadHash FROM dbo.ProcessedOperations WHERE BusinessId=@BusinessId AND Id=@Id", new { request.BusinessId, op.Id }, tx);
            if (previous is not null)
            {
                await Authorize(c, tx, user, request.BusinessId, true);
                if (previous != hash) throw new DomainException("conflict", "An operation ID was reused with different content.", 409);
                continue;
            }
            if (op.Type == "createBusiness")
            {
                Rules.Require(op.EntityId == request.BusinessId, "Invalid business identifier.");
                var input = Wire.Read<Business>(op.Payload);
                input.Id = request.BusinessId; input.OwnerUid = user; input.Name = Rules.Name(input.Name);
                input.DefaultLocation = Rules.Name(input.DefaultLocation); input.Currency = "PHP"; input.Timezone = "Asia/Manila";
                input.CreatedAt = input.UpdatedAt = DateTimeOffset.UtcNow;
                // The unique owner constraint resolves simultaneous onboarding on two devices.
                await c.ExecuteAsync("INSERT dbo.Businesses(Id,OwnerUid,Name,DefaultLocation,Currency,Timezone,CreatedAt,UpdatedAt) VALUES(@Id,@OwnerUid,@Name,@DefaultLocation,@Currency,@Timezone,@CreatedAt,@UpdatedAt)", input, tx);
                await c.ExecuteAsync("INSERT dbo.BusinessMembers(BusinessId,UserId,Role) VALUES(@Id,@user,'Owner')", new { input.Id, user }, tx);
                await repository.Log("business", input.Id, input);
            }
            else
            {
                await Authorize(c, tx, user, request.BusinessId, true, op.Type is "saveInventoryItem" or "saveProduct" or "adjustStock" or "deleteExpense");
                await handler.Execute(op);
            }
            await c.ExecuteAsync("INSERT dbo.ProcessedOperations(BusinessId,Id,UserId,PayloadHash) VALUES(@BusinessId,@Id,@user,@hash)", new { request.BusinessId, op.Id, user, hash }, tx);
        }
        await tx.CommitAsync();
    }
    public async Task<PullResult> Pull(Guid user, Guid business, long cursor)
    {
        Rules.Require(cursor >= 0, "Invalid sync cursor.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        await Lock(c, tx, business); await Authorize(c, tx, user, business);
        var rows = (await c.QueryAsync<ChangeRow>("SELECT TOP(501) [Cursor],CollectionName,EntityId,Payload FROM dbo.SyncChanges WHERE BusinessId=@business AND [Cursor]>@cursor ORDER BY [Cursor]", new { business, cursor }, tx)).ToList();
        var result = rows.Take(500).Select(r =>
        {
            using var document = JsonDocument.Parse(r.Payload);
            return new Change(r.Cursor, r.CollectionName, r.EntityId, document.RootElement.Clone());
        }).ToList();
        await tx.CommitAsync();
        return new PullResult(result.LastOrDefault()?.Cursor ?? cursor, result, rows.Count > 500);
    }
    private sealed record ChangeRow(long Cursor, string CollectionName, Guid EntityId, string Payload);
}
