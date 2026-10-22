namespace Ruach.BusinessAid.Shared;

public sealed record AdminBusinessTotal(Guid Id, string Name, long RevenueCentavos, long CostCentavos, long ExpenseCentavos, long ProfitCentavos);
public sealed record AdminAnalytics(FinancialSummary Summary, List<SalesTrend> Trend, List<AdminBusinessTotal> TopBusinesses);
public sealed record AdminMember(Guid Uid, string DisplayName, string Email, string Role);
public sealed record AdminRecords(AppData Data, int Total, int Page, int PageSize);
public sealed record AdminMembership(Guid BusinessId, string Name, string Role);
public sealed class AdminDirectory
{
    public List<AdminUser> Users { get; set; } = [];
    public List<Business> Businesses { get; set; } = [];
    public List<AdminBusinessTotal> Totals { get; set; } = [];
    public int Total { get; set; }
}
