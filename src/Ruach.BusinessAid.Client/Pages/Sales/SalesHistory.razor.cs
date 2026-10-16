using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class SalesHistory
{
    private DateTime? from, to;
    private string FromLimit => from?.ToString("yyyy-MM-dd") ?? "";
    private string ToLimit => to?.ToString("yyyy-MM-dd") ?? Today().ToString("yyyy-MM-dd");
    private bool InvalidRange => (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        || (from.HasValue && DateOnly.FromDateTime(from.Value) > Today())
        || (to.HasValue && DateOnly.FromDateTime(to.Value) > Today());
    private IEnumerable<Sale> FilteredSales => Store.Sales
        .Where(x => (from is null || string.CompareOrdinal(x.SaleDate, FromLimit) >= 0)
            && (to is null || string.CompareOrdinal(x.SaleDate, to.Value.ToString("yyyy-MM-dd")) <= 0))
        .OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

    private void ClearDates() { from = null; to = null; }
}
