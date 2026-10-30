using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class SalesHistory
{
    private DateTime? from, to;
    private string period = "month"; private int filterRevision;
    protected override void OnInitialized() { base.OnInitialized(); SetPeriod(); }
    private void CustomDates() => period = "custom";
    private void SetPeriod()
    {
        filterRevision++;
        var today = Today().ToDateTime(TimeOnly.MinValue);
        if (period == "all") { ClearDates(); return; }
        if (period == "custom") { from = new DateTime(today.Year, today.Month, 1); to = today; return; }
        to = period == "yesterday" ? today.AddDays(-1) : today;
        from = period switch {
            "yesterday" => to,
            "week" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
            "month" => new DateTime(today.Year, today.Month, 1),
            _ => today
        };
    }
    private string FromLimit => from?.ToString("yyyy-MM-dd") ?? "2026-01-01";
    private string ToLimit => to?.ToString("yyyy-MM-dd") ?? Today().ToString("yyyy-MM-dd");
    private bool InvalidRange => (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        || (from.HasValue && DateOnly.FromDateTime(from.Value) > Today())
        || (to.HasValue && DateOnly.FromDateTime(to.Value) > Today());
    private IEnumerable<Sale> FilteredSales => Store.Sales
        .Where(x => (from is null || string.CompareOrdinal(x.SaleDate, FromLimit) >= 0)
            && (to is null || string.CompareOrdinal(x.SaleDate, to.Value.ToString("yyyy-MM-dd")) <= 0))
        .OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

    private void ClearDates() { filterRevision++; from = null; to = null; period = "all"; }
}
