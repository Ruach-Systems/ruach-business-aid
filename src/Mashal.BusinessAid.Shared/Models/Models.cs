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

public sealed record AppUser(Guid Uid, string DisplayName, string Email, string? PhotoURL)
{
    public bool IsDemo { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsAdmin { get; init; }
}

public sealed class Item : Entity
{
    public string Name { get; set; } = "";
    public long SellingPriceCentavos { get; set; }
    public long UnitCostCentavos { get; set; }
    public long CurrentQuantity { get; set; }
    public long MinimumQuantity { get; set; }
    public bool IsActive { get; set; }
}

public class SaleLine
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public long Quantity { get; set; }
    public long UnitPriceCentavos { get; set; }
    public long UnitCostCentavos { get; set; }
    public long LineRevenueCentavos { get; set; }
    public long LineCostCentavos { get; set; }
}

public sealed class Sale : Entity
{
    public string SaleDate { get; set; } = "";
    public string Location { get; set; } = "";
    public long TotalItems { get; set; }
    public long TotalRevenueCentavos { get; set; }
    public long TotalCostCentavos { get; set; }
    public long TotalProfitCentavos { get; set; }
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
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public long Quantity { get; set; }
    public long UnitCostCentavos { get; set; }
    public long TotalCostCentavos { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? Note { get; set; }
}

public sealed class StockMovement : Entity
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public long QuantityDelta { get; set; }
    public long BalanceAfter { get; set; }
    public long UnitCostCentavos { get; set; }
    public string MovementType { get; set; } = "";
    public string ReferenceType { get; set; } = "";
    public Guid ReferenceId { get; set; }
    public string? Note { get; set; }
}

public sealed class AppData
{
    public Business? Business { get; set; }
    public List<Item> Items { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<StockReceipt> Receipts { get; set; } = [];
    public List<StockMovement> Movements { get; set; } = [];
}
