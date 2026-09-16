using System.Text.Json;
namespace Mashal.BusinessAid.Shared;

public static class LocalData
{
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Wire.Json), Wire.Json)!;
    public static IEnumerable<Entity> Entities(AppData data) => data.InventoryItems.Cast<Entity>().Concat(data.Products).Concat(data.Expenses).Concat(data.Sales).Concat(data.Batches).Concat(data.Receipts).Concat(data.Movements);
    public static string Collection(Entity entity) => entity switch { InventoryItem => "inventoryItems", Product => "products", Expense => "expenses", Sale => "sales", ProductionBatch => "batches", StockReceipt => "receipts", InventoryMovement => "movements", _ => throw new InvalidOperationException() };
    public static void Apply(AppData data, string collection, JsonElement value)
    {
        switch (collection)
        {
            case "business": data.Business = Wire.Read<Business>(value); break;
            case "inventoryItems": Put(data.InventoryItems, Wire.Read<InventoryItem>(value)); break;
            case "products": Put(data.Products, Wire.Read<Product>(value)); break;
            case "expenses": Put(data.Expenses, Wire.Read<Expense>(value)); break;
            case "sales": Put(data.Sales, Wire.Read<Sale>(value)); break;
            case "batches": Put(data.Batches, Wire.Read<ProductionBatch>(value)); break;
            case "receipts": Put(data.Receipts, Wire.Read<StockReceipt>(value)); break;
            case "movements": Put(data.Movements, Wire.Read<InventoryMovement>(value)); break;
            default: throw new InvalidOperationException("Unknown synchronized collection.");
        }
    }
    private static void Put<T>(List<T> list, T entity) where T : Entity { var index = list.FindIndex(x => x.Id == entity.Id); if (index < 0) list.Add(entity); else list[index] = entity; }
    public static string? Version(AppData data, Guid id) { var version = Entities(data).FirstOrDefault(x => x.Id == id)?.Version; return version is null ? null : Convert.ToBase64String(version); }
}
public sealed record Projection(string CollectionName, JsonElement Entity);
public sealed class LocalRepository(AppData data) : IEntityRepository
{
    public List<Projection> Changes { get; } = [];
    public Task<T?> Find<T>(Guid id) where T : Entity => Task.FromResult(LocalData.Entities(data).OfType<T>().FirstOrDefault(x => x.Id == id && x.DeletedAt is null));
    public async Task<T> Required<T>(Guid id) where T : Entity => await Find<T>(id) ?? throw new DomainException("not_found", "The referenced record is unavailable.", 404);
    public Task Save(Entity entity)
    {
        entity.BusinessId = data.Business!.Id;
        entity.CreatedAt = entity.CreatedAt == default ? DateTimeOffset.UtcNow : entity.CreatedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var collection = LocalData.Collection(entity);
        if (entity is InventoryItem or Product)
        {
            var name = entity is InventoryItem item ? item.Name : ((Product)(Entity)entity).Name;
            Rules.Require(!LocalData.Entities(data).Any(x => x.GetType() == entity.GetType() && x.Id != entity.Id && x.DeletedAt is null && Rules.Name(x is InventoryItem i ? i.Name : ((Product)x).Name).Equals(name, StringComparison.OrdinalIgnoreCase)), $"A record named “{name}” already exists.");
        }
        var json = JsonSerializer.SerializeToElement(entity, entity.GetType(), Wire.Json);
        LocalData.Apply(data, collection, json);
        Changes.RemoveAll(x => x.CollectionName == collection && x.Entity.GetProperty("id").GetGuid() == entity.Id);
        Changes.Add(new(collection, json));
        return Task.CompletedTask;
    }
}
