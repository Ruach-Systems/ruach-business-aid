using Dapper;
using Mashal.BusinessAid.Shared;
using Microsoft.Data.SqlClient;
namespace Mashal.BusinessAid.Api.Data;

// Low-volume provisioning shares a transaction-owned lock. Phone claims, transfers and
// decisions cannot race each other; unique indexes remain the final database safeguard.
public sealed class AccountService(SqlConnectionFactory connections, IdentityRepository identities)
{
    private static async Task Lock(SqlConnection c, SqlTransaction tx)
    {
        var result = await c.ExecuteScalarAsync<int>("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='mashal-account-management',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; SELECT @r;", transaction: tx);
        if (result < 0) throw new DomainException("retry", "Account management is busy. Try again shortly.", 503);
    }
    public async Task RequireAdmin(Guid actor)
    {
        if ((await identities.Find(actor))?.IsAdmin != true) throw new DomainException("forbidden", "Mashal Admin access is required.", 403);
    }
    public async Task<AccountOverview> Overview(Guid user)
    {
        var account = await identities.Find(user) ?? throw new DomainException("unauthorized", "Sign in again.", 401);
        await using var c = await connections.Open();
        var businesses = (await c.QueryAsync<Business>("SELECT b.* FROM dbo.Businesses b JOIN dbo.BusinessMembers m ON m.BusinessId=b.Id WHERE m.UserId=@user ORDER BY b.Name,b.Id", new { user })).ToList();
        var requests = (await c.QueryAsync<BusinessRequest>("SELECT * FROM dbo.BusinessRequests WHERE OwnerUid=@user ORDER BY CreatedAt DESC,Id", new { user })).ToList();
        return new(account, businesses, requests);
    }
    private static async Task SetPhone(SqlConnection c, SqlTransaction tx, Guid user, string phone)
    {
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users WHERE PhoneNumber=@phone AND Id<>@user", new { phone, user }, tx) != 0)
            throw new DomainException("phone_in_use", "This mobile number is already linked to another account. Use another number or contact Mashal Admin.", 409);
        if (await c.ExecuteAsync("UPDATE dbo.Users SET PhoneNumber=@phone WHERE Id=@user", new { phone, user }, tx) != 1)
            throw new DomainException("unauthorized", "Sign in again.", 401);
    }
    public async Task SavePhone(Guid user, PhoneInput input)
    {
        var phone = PhilippinePhone.Normalize(input.PhoneNumber);
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction();
        await Lock(c, tx); await SetPhone(c, tx, user, phone); await tx.CommitAsync();
    }
    public async Task Request(Guid user, BusinessRequestInput input)
    {
        Rules.Require(input.Id != Guid.Empty, "A request identifier is required.");
        var name = Rules.Name(input.Name); var location = Rules.Name(input.DefaultLocation);
        Rules.Require(name.Length <= 160 && location.Length <= 500, "Business name or location is too long.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction(); await Lock(c, tx);
        var existing = await c.QuerySingleOrDefaultAsync<BusinessRequest>("SELECT * FROM dbo.BusinessRequests WHERE Id=@Id", input, tx);
        if (existing is not null)
        {
            if (existing.OwnerUid != user || existing.Name != name || existing.DefaultLocation != location)
                throw new DomainException("conflict", "This request identifier has already been used.", 409);
            await tx.CommitAsync(); return; // Lost response: retry does not create a second request.
        }
        if (input.PhoneNumber is not null) await SetPhone(c, tx, user, PhilippinePhone.Normalize(input.PhoneNumber));
        var phone = await c.ExecuteScalarAsync<string?>("SELECT PhoneNumber FROM dbo.Users WHERE Id=@user", new { user }, tx);
        if (phone is null) throw new DomainException("phone_required", "Add a unique Philippine mobile number before requesting a business.", 403);
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.BusinessRequests WHERE OwnerUid=@user AND Status='Pending'", new { user }, tx) != 0)
            throw new DomainException("pending_request", "You already have a pending business request. Wait for its review before submitting another.", 409);
        await c.ExecuteAsync("INSERT dbo.BusinessRequests(Id,OwnerUid,Name,DefaultLocation,Status) VALUES(@Id,@user,@name,@location,'Pending')", new { input.Id, user, name, location }, tx);
        await tx.CommitAsync();
    }
    public async Task Decide(Guid actor, Guid id, DecisionInput input)
    {
        await RequireAdmin(actor);
        var reason = (input.Reason ?? "").Trim();
        Rules.Require(reason.Length <= 1000 && (input.Approve || reason.Length > 0), "A rejection reason of up to 1,000 characters is required.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction(); await Lock(c, tx);
        var request = await c.QuerySingleOrDefaultAsync<BusinessRequest>("SELECT * FROM dbo.BusinessRequests WHERE Id=@id", new { id }, tx)
            ?? throw new DomainException("not_found", "Request not found.", 404);
        var status = input.Approve ? "Approved" : "Rejected";
        if (request.Status != "Pending")
        {
            if (request.Status == status && request.DecidedBy == actor && (request.Reason ?? "") == reason) { await tx.CommitAsync(); return; }
            throw new DomainException("conflict", "This request was already decided. Refresh before continuing.", 409);
        }
        Guid? business = null;
        if (input.Approve)
        {
            var phone = await c.ExecuteScalarAsync<string?>("SELECT PhoneNumber FROM dbo.Users WHERE Id=@OwnerUid", request, tx);
            Rules.Require(phone is not null, "The owner must add a unique mobile number before approval.");
            business = request.Id;
            var value = new Business { Id = business.Value, OwnerUid = request.OwnerUid, Name = request.Name, DefaultLocation = request.DefaultLocation, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            await c.ExecuteAsync("INSERT dbo.Businesses(Id,OwnerUid,Name,DefaultLocation,Currency,Timezone,CreatedAt,UpdatedAt) VALUES(@Id,@OwnerUid,@Name,@DefaultLocation,@Currency,@Timezone,@CreatedAt,@UpdatedAt)", value, tx);
            await c.ExecuteAsync("INSERT dbo.BusinessMembers(BusinessId,UserId,Role) VALUES(@Id,@OwnerUid,'Owner')", value, tx);
            await new EntityRepository(c, tx, business.Value).Log("business", business.Value, value);
        }
        await c.ExecuteAsync("UPDATE dbo.BusinessRequests SET Status=@status,Reason=@reason,BusinessId=@business,DecidedBy=@actor,DecidedAt=SYSUTCDATETIME() WHERE Id=@id", new { status, reason, business, actor, id }, tx);
        await c.ExecuteAsync("INSERT dbo.AdminAudit(Id,ActorUid,Action,SubjectUid,RequestId,Reason) VALUES(NEWID(),@actor,@status,@OwnerUid,@id,@reason)", new { actor, status, request.OwnerUid, id, reason }, tx);
        await tx.CommitAsync();
    }
    public async Task Transfer(Guid actor, PhoneTransferInput input)
    {
        await RequireAdmin(actor);
        Rules.Require(input.IdentityChecked && input.FromUserId != input.ToUserId && input.ToUserId != Guid.Empty, "Confirm the offline identity check and choose a different recipient account.");
        var phone = PhilippinePhone.Normalize(input.PhoneNumber); var reason = (input.Reason ?? "").Trim();
        Rules.Require(reason.Length is > 0 and <= 1000, "Record an identity-review reason of up to 1,000 characters.");
        await using var c = await connections.Open(); await using var tx = c.BeginTransaction(); await Lock(c, tx);
        var rows = (await c.QueryAsync<PhoneRow>("SELECT Id,PhoneNumber FROM dbo.Users WHERE Id IN (@FromUserId,@ToUserId)", input, tx)).ToList();
        var from = rows.Find(x => x.Id == input.FromUserId); var to = rows.Find(x => x.Id == input.ToUserId);
        if (from is null || to is null || from.PhoneNumber != phone || to.PhoneNumber != input.ExpectedRecipientPhone)
            throw new DomainException("conflict", "Phone ownership changed. Refresh both accounts and review the transfer again.", 409);
        await c.ExecuteAsync("UPDATE dbo.Users SET PhoneNumber=NULL WHERE Id=@FromUserId; UPDATE dbo.Users SET PhoneNumber=@phone WHERE Id=@ToUserId;", new { input.FromUserId, input.ToUserId, phone }, tx);
        await c.ExecuteAsync("INSERT dbo.AdminAudit(Id,ActorUid,Action,SubjectUid,RecipientUid,PhoneNumber,PreviousRecipientPhone,Reason) VALUES(NEWID(),@actor,'PhoneTransfer',@FromUserId,@ToUserId,@phone,@previous,@reason)", new { actor, input.FromUserId, input.ToUserId, phone, previous = to.PhoneNumber, reason }, tx);
        await tx.CommitAsync();
    }
    public async Task<AdminOverview> Admin(Guid actor)
    {
        await RequireAdmin(actor); await using var c = await connections.Open();
        // Explicit metadata projections: never serialize operational entity repositories here.
        var users = (await c.QueryAsync<AdminUser>("SELECT u.Id Uid,u.DisplayName,u.Email,u.PhoneNumber,u.CreatedAt,(SELECT COUNT(*) FROM dbo.Businesses b WHERE b.OwnerUid=u.Id) BusinessCount FROM dbo.Users u ORDER BY u.CreatedAt DESC")).ToList();
        var businesses = (await c.QueryAsync<Business>("SELECT Id,OwnerUid,Name,DefaultLocation,Currency,Timezone,CreatedAt,UpdatedAt FROM dbo.Businesses ORDER BY CreatedAt DESC")).ToList();
        var requests = (await c.QueryAsync<BusinessRequest>("SELECT * FROM dbo.BusinessRequests ORDER BY CreatedAt DESC")).ToList();
        var audit = (await c.QueryAsync<AdminAuditEntry>("SELECT TOP(200) * FROM dbo.AdminAudit ORDER BY CreatedAt DESC")).ToList();
        return new(users, businesses, requests, audit);
    }
    private sealed record PhoneRow(Guid Id, string? PhoneNumber);
}
