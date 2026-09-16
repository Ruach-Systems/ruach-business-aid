using System.Text.Json;
namespace Mashal.BusinessAid.Shared;

public sealed record Operation(Guid Id, Guid EntityId, string Type, string? ExpectedVersion, JsonElement Payload);
public sealed record PushRequest(Guid BusinessId, List<Operation> Operations);
public sealed record Change(long Cursor, string CollectionName, Guid EntityId, JsonElement Payload);
public sealed record PullResult(long Cursor, List<Change> Changes, bool HasMore);
public sealed record BootstrapResult(AppUser User, AppData Data, long Cursor);
public interface IBusinessService
{
    Task<BootstrapResult> Bootstrap(Guid user);
    Task Push(Guid user, PushRequest request);
    Task<PullResult> Pull(Guid user, Guid business, long cursor);
}
public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static T Read<T>(JsonElement value) => value.Deserialize<T>(Json) ?? throw new DomainException("validation", "Invalid operation payload.");
}
