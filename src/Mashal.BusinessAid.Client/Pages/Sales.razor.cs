using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Sales
{
    private Dictionary<Guid, decimal> quantities = []; private DateTime date = Today().ToDateTime(TimeOnly.MinValue); private bool deduct = true; private string message = "";
    private void Ensure(Guid id) { quantities.TryAdd(id, 0); }
    private void Change(Guid id, int amount) => quantities[id] = Math.Max(0, quantities.GetValueOrDefault(id) + amount);
    private long Revenue => Store.Products.Sum(p => Rules.Round(quantities.GetValueOrDefault(p.Id) * p.SellingPriceCentavos));
    private long Cost => Store.Products.Sum(p => Rules.Round(quantities.GetValueOrDefault(p.Id) * ProductCost(p, Store.Inventory)));
    private Task Save() => Run(async () => { await Store.Execute("recordSale", Guid.NewGuid(), new SaleInput(new(quantities), deduct, date.ToString("yyyy-MM-dd"), Store.Products.ToDictionary(x => x.Id, x => x.SellingPriceCentavos))); quantities.Clear(); message = "Sale saved on this device."; });

}
