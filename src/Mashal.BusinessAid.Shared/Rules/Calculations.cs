using System.Globalization;

namespace Mashal.BusinessAid.Shared;
public static class Calculations
{
    public static string FriendlyDate(string value, DateOnly today)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return value;
        if (date == today)
            return "Today";
        if (date == today.AddDays(-1))
            return "Yesterday";
        return date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    public static decimal PerItemQuantity(decimal batchQuantity, decimal batchYield) => batchYield > 0 ? batchQuantity / batchYield : 0;
    public static decimal BatchQuantity(decimal perItemQuantity, decimal batchYield) => perItemQuantity * Math.Max(1, batchYield);
    public static long Centavos(decimal pesos) => Rules.Round(pesos * 100);
    public static string Money(long value) => (value / 100m).ToString("C2", CultureInfo.GetCultureInfo("en-PH"));
    public static string Quantity(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public static string UnitName(string? unit, decimal quantity = 2)
    {
        var singular = Math.Abs(quantity) == 1;
        return unit switch
        {
            "g" => singular ? "gram" : "grams",
            "kg" => singular ? "kilogram" : "kilograms",
            "ml" => singular ? "milliliter" : "milliliters",
            "l" => singular ? "liter" : "liters",
            "pc" => singular ? "piece" : "pieces",
            "pack" => singular ? "pack" : "packs",
            "serving" => singular ? "serving" : "servings",
            _ => unit ?? ""
        };
    }

    public static long ProductCost(Product product, IEnumerable<InventoryItem> inventory)
    {
        var items = inventory.ToList();
        if (product.InventoryMode != "prepared")
            return product.ManualCostCentavos;
        var finished = items.Find(x => x.Id == product.FinishedInventoryItemId)?.AverageCostCentavos ?? 0;
        return finished > 0 ? finished : product.Recipe.Sum(x => Rules.Round(x.Quantity * (items.Find(i => i.Id == x.InventoryItemId)?.AverageCostCentavos ?? 0)));
    }

    public static DateOnly Today(DateTimeOffset? now = null) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).DateTime);
    public static (string From, string To) Period(string period, DateTimeOffset? now = null)
    {
        var today = Today(now);
        var from = period switch
        {
            "week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7),
            "month" => new DateOnly(today.Year, today.Month, 1),
            "year" => new DateOnly(today.Year, 1, 1),
            _ => today
        };
        return (from.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));
    }

    public static decimal Conversion(string unit, string baseUnit) => unit == baseUnit ? 1 : (unit, baseUnit) switch
    {
        ("kg", "g") => 1000,
        ("L", "ml") => 1000,
        _ => throw new DomainException("validation", "The unit does not match the inventory base unit.")};
    public static string Route(bool ready, bool signedIn, bool hasBusiness, string path)
    {
        if (!ready)
            return path;
        if (!signedIn)
            return "/sign-in";
        if (!hasBusiness)
            return "/onboarding";
        return path is "/" or "/sign-in" or "/onboarding" ? "/dashboard" : path;
    }

    public static string AccountRoute(bool ready, bool signedIn, bool hasBusiness, bool phoneRequired, bool admin, bool newOwner, string path)
    {
        if (!ready)
            return path;
        if (!signedIn)
            return "/sign-in";
        if (path.StartsWith("/admin", StringComparison.Ordinal))
            return admin ? path : "/businesses";
        if (path is "/businesses" or "/settings")
            return path;
        if (path == "/onboarding")
            return newOwner ? path : "/businesses";
        if (newOwner)
            return admin ? "/admin" : "/onboarding";
        if (phoneRequired || !hasBusiness)
            return "/businesses";
        return path is "/" or "/sign-in" ? "/dashboard" : path;
    }
}

public sealed record ReceiptInput(Guid InventoryItemId, decimal Quantity, string Unit, decimal ToBase, long TotalCostCentavos, string? Note);
public sealed record AdjustmentInput(Guid InventoryItemId, decimal CountedQuantity, string Reason, string? Note);
public sealed record SaleInput(Dictionary<Guid, decimal> Quantities, bool DeductInventory, string SaleDate, Dictionary<Guid, long>? Prices);
public sealed record BatchInput(Guid ProductId, decimal PlannedYield, string? Note);
public sealed record CompleteInput(decimal ActualYield, Dictionary<Guid, decimal> ActualQuantities, string? Note);
public sealed record FinancialSummary(long RevenueCentavos, long CostCentavos, long ExpenseCentavos, long GrossProfitCentavos, long ProfitCentavos, int Transactions, decimal ItemCount);
public sealed record ProductReport(Guid ProductId, string ProductName, decimal Quantity, long RevenueCentavos, long CostCentavos, long ProfitCentavos);
public sealed record ExpenseReport(string Category, long AmountCentavos, decimal? Percentage);
public sealed record SalesTrend(string Period, long RevenueCentavos, long CostCentavos, long GrossProfitCentavos);
