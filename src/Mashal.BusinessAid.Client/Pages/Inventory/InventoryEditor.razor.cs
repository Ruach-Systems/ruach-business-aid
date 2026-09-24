using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class InventoryEditor
{
    [Parameter] public Guid? Id { get; set; }
    private Item draft = new() { Id = Guid.NewGuid(), IsActive = true };
    private decimal price, cost;
    private string? version;
    private Guid? loaded;
    private long PriceCentavos => Centavos(price);
    private long CostCentavos => Centavos(cost);
    private static string MarkupText(long price, long unitCost)
    {
        var markup = Markup(price, unitCost);
        return markup is null ? "-" : $"{markup:0.0}%";
    }

    protected override void OnParametersSet()
    {
        if (loaded == Id) return;
        loaded = Id;
        draft = LocalData.Clone(Store.Data.Items.Find(x => x.Id == Id) ?? new Item { Id = Guid.NewGuid(), IsActive = true });
        version = draft.Version is { } value ? Convert.ToBase64String(value) : null;
        price = draft.SellingPriceCentavos / 100m;
        cost = draft.UnitCostCentavos / 100m;
    }

    private Task Save() => Run(async () =>
    {
        draft.SellingPriceCentavos = PriceCentavos;
        draft.UnitCostCentavos = CostCentavos;
        await Store.Execute("saveItem", draft.Id, draft, version);
    }, "/items");
}
