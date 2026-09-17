using System.Text.Json;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Services;
public sealed partial class BusinessState
{
    public async Task Execute(string type, Guid id, object payload, string? expectedVersion = null)
    {
        var workspace = State?.StorageKey;
        await gate.WaitAsync();
        try
        {
            if (workspace != State?.StorageKey)
                throw new InvalidOperationException("The selected business changed. Open the form again in the intended business.");
            if (State?.Data.Business is null || PhoneRequired)
                throw new InvalidOperationException("Open an approved business first.");
            Rules.Require(type != "createBusiness", "Business creation requires admin approval.");
            var json = JsonSerializer.SerializeToElement(payload, Wire.Json);
            State = await storage.Mutate(State, async latest =>
            {
                var target = type == "adjustStock" ? json.GetProperty("inventoryItemId").GetGuid() : id;
                var op = new PendingOperation
                {
                    Id = Guid.NewGuid(),
                    EntityId = id,
                    Type = type,
                    Payload = json,
                    ExpectedVersion = expectedVersion
                };
                op.DependsOnPending = latest.Outbox.Any(x => x.EntityId == target || x.Projections.Any(p => p.Entity.GetProperty("id").GetGuid() == target));
                var projected = LocalData.Clone(latest.Data);
                var repository = new LocalRepository(projected);
                await new CommandProcessor(repository, checkConcurrency: false).Execute(op.Command()with { ExpectedVersion = LocalData.Version(projected, target) });
                op.Projections = repository.Changes;
                latest.Outbox.Add(op);
                latest.Rebuild();
            });
        }
        finally
        {
            gate.Release();
            Notify();
        }

        _ = Synchronize();
    }

    public async Task CreateLocalBusiness(string name, string location)
    {
        if (!Development || User?.IsDemo != true)
            throw new InvalidOperationException("Local workspace creation is development-only.");
        var value = new Business
        {
            Id = Guid.NewGuid(),
            OwnerUid = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = Rules.Name(name),
            DefaultLocation = Rules.Name(location),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        State = new()
        {
            User = User,
            ServerData = new()
            {
                Business = value
            }
        };
        State.Rebuild();
        await storage.Write("accounts", User.Uid, State);
        Notify();
    }

    public async Task SaveProduct(Product value, string? version)
    {
        var workspace = State?.StorageKey;
        value.Name = Rules.Name(value.Name);
        Rules.Require(!Data.Products.Any(x => x.Id != value.Id && x.DeletedAt is null && Rules.Name(x.Name).Equals(value.Name, StringComparison.OrdinalIgnoreCase)), "A product with this name already exists.");
        if (value.InventoryMode == "prepared" && value.FinishedInventoryItemId is null)
        {
            var item = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = value.Name,
                BaseUnit = "pc",
                UnitKind = "count",
                IsActive = true,
                MinimumQuantity = 5
            };
            await Execute("saveInventoryItem", item.Id, item);
            value.FinishedInventoryItemId = item.Id;
        }

        if (value.InventoryMode == "untracked")
        {
            value.Recipe = [];
            value.RecipeBatchYield = 1;
        }

        if (workspace != State?.StorageKey)
            throw new InvalidOperationException("The selected business changed. Open the product again in the intended business.");
        await Execute("saveProduct", value.Id, value, version);
    }
}
