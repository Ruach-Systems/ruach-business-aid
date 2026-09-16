namespace Mashal.BusinessAid.Shared;

public abstract class Entity
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public byte[]? Version { get; set; }
}
public sealed class Business
{
    public Guid Id { get; set; }
    public Guid OwnerUid { get; set; }
    public string Name { get; set; } = "";
    public string DefaultLocation { get; set; } = "";
    public string Currency { get; set; } = "PHP";
    public string Timezone { get; set; } = "Asia/Manila";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed record AppUser(Guid Uid, string DisplayName, string Email, string? PhotoURL) { public bool IsDemo { get; init; } }
public class RecipeLine
{
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
}
public class SaleLine
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public long UnitPriceCentavos { get; set; }
    public long UnitCostCentavos { get; set; }
    public long LineRevenueCentavos { get; set; }
    public long LineCostCentavos { get; set; }
}
public class BatchIngredient
{
    public Guid InventoryItemId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal PlannedQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public long UnitCostCentavos { get; set; }
}
public sealed class InventoryItem : Entity
{
    public string Name { get; set; } = "";
    public string BaseUnit { get; set; } = "";
    public string UnitKind { get; set; } = "";
    public decimal CurrentQuantity { get; set; }
    public long AverageCostCentavos { get; set; }
    public decimal MinimumQuantity { get; set; }
    public bool IsActive { get; set; }
}
public sealed class Product : Entity
{
    public string Name { get; set; } = "";
    public long SellingPriceCentavos { get; set; }
    public long ManualCostCentavos { get; set; }
    public string InventoryMode { get; set; } = "";
    public Guid? FinishedInventoryItemId { get; set; }
    public decimal RecipeBatchYield { get; set; } = 1;
    public bool IsActive { get; set; }
    public List<RecipeLine> Recipe { get; set; } = [];
}
public sealed class Sale : Entity
{
    public string SaleDate { get; set; } = "";
    public string Location { get; set; } = "";
    public decimal TotalItems { get; set; }
    public long TotalRevenueCentavos { get; set; }
    public long TotalCostCentavos { get; set; }
    public long TotalProfitCentavos { get; set; }
    public bool DeductInventory { get; set; }
    public List<SaleLine> Lines { get; set; } = [];
}
public sealed class Expense : Entity
{
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public long AmountCentavos { get; set; }
    public string ExpenseDate { get; set; } = "";
}
public sealed class StockReceipt : Entity
{
    public Guid InventoryItemId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal QuantityInBaseUnit { get; set; }
    public long TotalCostCentavos { get; set; }
    public long ResultingAverageCostCentavos { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? Note { get; set; }
}
public sealed class InventoryMovement : Entity
{
    public Guid InventoryItemId { get; set; }
    public string ItemName { get; set; } = "";
    public decimal QuantityDelta { get; set; }
    public decimal BalanceAfter { get; set; }
    public long UnitCostCentavos { get; set; }
    public string MovementType { get; set; } = "";
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public string? Note { get; set; }
}
public sealed class ProductionBatch : Entity
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal PlannedYield { get; set; }
    public decimal? ActualYield { get; set; }
    public long? TotalCostCentavos { get; set; }
    public long? CostPerUnitCentavos { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Note { get; set; }
    public List<BatchIngredient> Ingredients { get; set; } = [];
}
public sealed class AppData
{
    public Business? Business { get; set; }
    public List<InventoryItem> InventoryItems { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<StockReceipt> Receipts { get; set; } = [];
    public List<InventoryMovement> Movements { get; set; } = [];
    public List<ProductionBatch> Batches { get; set; } = [];
}
