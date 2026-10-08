using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Sales
{
    private Dictionary<Guid,long> quantities = [];
    private DateTime date = Today().ToDateTime(TimeOnly.MinValue);
    private string message = "";
    private string search = "";
    private bool Matches(Item item) => item.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    private IEnumerable<Item> VisibleItems => Store.Items.Where(x => Matches(x) || quantities.GetValueOrDefault(x.Id) > 0);
    private int MatchCount => Store.Items.Count(Matches);
    private int SelectedHiddenByName => Store.Items.Count(x => !Matches(x) && quantities.GetValueOrDefault(x.Id) > 0);
    private void ClearSearch() => search = "";
    private void Ensure(Guid id) => quantities.TryAdd(id, 0);
    private void Change(Guid id, int amount) => quantities[id] = Math.Max(0, quantities.GetValueOrDefault(id) + amount);
    private long TotalItems => quantities.Values.Sum();
    private long Revenue => Store.Items.Sum(x => checked(quantities.GetValueOrDefault(x.Id) * x.SellingPriceCentavos));
    private long Cost => Store.Items.Sum(x => checked(quantities.GetValueOrDefault(x.Id) * x.UnitCostCentavos));
    private bool HasNegativeResult => Store.Items.Any(x => quantities.GetValueOrDefault(x.Id) > 0 && quantities.GetValueOrDefault(x.Id) > x.CurrentQuantity);
    private Task Save() => Run(async () =>
    {
        var lines = Store.Items.Where(x => quantities.GetValueOrDefault(x.Id) > 0)
            .Select(x => new SaleLineInput(x.Id, quantities[x.Id])).ToList();
        await Store.Execute("recordSale", Guid.NewGuid(), new SaleInput(lines, date.ToString("yyyy-MM-dd")));
        quantities.Clear();
        search = "";
        message = "Sale saved on this device.";
    });
}
