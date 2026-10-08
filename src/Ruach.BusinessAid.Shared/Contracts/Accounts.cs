namespace Ruach.BusinessAid.Shared;
public sealed record PhoneInput(string PhoneNumber);
public sealed record BusinessRequestInput(Guid Id, string Name, string DefaultLocation, string? PhoneNumber = null);
public sealed record DecisionInput(bool Approve, string? Reason);
public sealed record PhoneTransferInput(Guid FromUserId, Guid ToUserId, string PhoneNumber, string Reason, bool IdentityChecked, string? ExpectedRecipientPhone);
public sealed class BusinessRequest
{
    public Guid Id { get; set; }
    public Guid OwnerUid { get; set; }
    public string Name { get; set; } = "";
    public string DefaultLocation { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? Reason { get; set; }
    public Guid? BusinessId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
}

public sealed record AccountOverview(AppUser User, List<Business> Businesses, List<BusinessRequest> Requests);
public sealed record AdminUser(Guid Uid, string DisplayName, string Email, string? PhoneNumber, DateTimeOffset CreatedAt, int BusinessCount);
public sealed class AdminAuditEntry
{
    public Guid Id { get; set; }
    public Guid ActorUid { get; set; }
    public string Action { get; set; } = "";
    public Guid SubjectUid { get; set; }
    public Guid? RecipientUid { get; set; }
    public Guid? RequestId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PreviousRecipientPhone { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record AdminOverview(List<AdminUser> Users, List<Business> Businesses, List<BusinessRequest> Requests, List<AdminAuditEntry> Audit);
