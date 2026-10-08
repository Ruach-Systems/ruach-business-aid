using System.Text.Json;
using Ruach.BusinessAid.Shared;
namespace Ruach.BusinessAid.Client.Services;

public sealed record ClientUser(string Uid, string DisplayName, string Email, string? PhotoURL, bool IsDemo = false)
{
    public string? PhoneNumber { get; init; }
    public bool IsAdmin { get; init; }
}
public sealed class PendingOperation
{
    public Guid Id { get; set; }
    public Guid EntityId { get; set; }
    public string Type { get; set; } = "";
    public string? ExpectedVersion { get; set; }
    public JsonElement Payload { get; set; }
    public List<Projection> Projections { get; set; } = [];
    public bool DependsOnPending { get; set; }
    public bool Prepared { get; set; }
    public string? Error { get; set; }
    public Operation Command() => new(Id, EntityId, Type, ExpectedVersion, Payload);
    public Guid VersionTarget => Type == "adjustStock" ? Payload.GetProperty("itemId").GetGuid() : EntityId;
}
public sealed class OfflineState
{
    public int ModelVersion { get; set; }
    public Guid? WorkspaceId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string StorageKey => WorkspaceId is { } id ? $"{User.Uid}:{id}" : User.Uid;
    public ClientUser User { get; set; } = new("", "", "", null);
    public AppData Data { get; set; } = new();
    public AppData ServerData { get; set; } = new();
    public long Cursor { get; set; }
    public long Revision { get; set; }
    public List<PendingOperation> Outbox { get; set; } = [];
    public void Rebuild()
    {
        Data = LocalData.Clone(ServerData);
        foreach (var op in Outbox)
        {
            if (op.Type == "createBusiness") Data.Business = Wire.Read<Business>(op.Payload);
            foreach (var projection in op.Projections) LocalData.Apply(Data, projection.CollectionName, projection.Entity);
        }
    }
}
