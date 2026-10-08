using System.Globalization;

namespace Ruach.BusinessAid.Shared;

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

    public static long Centavos(decimal pesos) => Rules.Round(pesos * 100);
    public static string Money(long value) => (value / 100m).ToString("C2", CultureInfo.GetCultureInfo("en-PH"));
    public static string Quantity(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public static long UnitProfit(long price, long cost) => price - cost;
    public static decimal? Markup(long price, long cost) => cost == 0 ? null : (price - cost) * 100m / cost;

    public static DateOnly Today(DateTimeOffset? now = null) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now ?? DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).DateTime);
    public static (string From, string To) Period(string period, DateTimeOffset? now = null)
    {
        var today = Today(now);
        var from = period switch
        {
            "week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7),
            "month" => new DateOnly(today.Year, today.Month, 1),
            _ => today
        };
        return (from.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));
    }

    public static string Route(bool ready, bool signedIn, bool hasBusiness, string path)
    {
        if (!ready) return path;
        if (!signedIn) return "/sign-in";
        if (!hasBusiness) return "/onboarding";
        return path is "/" or "/sign-in" or "/onboarding" ? "/dashboard" : path;
    }

    public static string AccountRoute(bool ready, bool signedIn, bool hasBusiness, bool phoneRequired, bool admin, bool newOwner, string path)
    {
        if (!ready) return path;
        if (!signedIn) return "/sign-in";
        if (path.StartsWith("/admin", StringComparison.Ordinal)) return admin ? path : "/businesses";
        if (path is "/businesses" or "/settings") return path;
        if (path == "/onboarding") return newOwner ? path : "/businesses";
        if (newOwner) return admin ? "/admin" : "/onboarding";
        if (phoneRequired || !hasBusiness) return "/businesses";
        return path is "/" or "/sign-in" ? "/dashboard" : path;
    }
}

public sealed record ReceiptInput(Guid ItemId, long Quantity, string? Note);
public sealed record AdjustmentInput(Guid ItemId, long CountedQuantity, string Reason, string? Note);
public sealed record SaleLineInput(Guid ItemId, long Quantity);
public sealed record SaleInput(List<SaleLineInput> Lines, string SaleDate);
public sealed record FinancialSummary(long RevenueCentavos, long CostCentavos, long ExpenseCentavos, long GrossProfitCentavos, long ProfitCentavos, int Transactions, long ItemCount);
public sealed record ItemReport(Guid ItemId, string ItemName, long Quantity, long RevenueCentavos, long CostCentavos, long ProfitCentavos);
public sealed record ExpenseReport(string Category, long AmountCentavos, decimal? Percentage);
public sealed record SalesTrend(string Period, long RevenueCentavos, long CostCentavos, long GrossProfitCentavos);
