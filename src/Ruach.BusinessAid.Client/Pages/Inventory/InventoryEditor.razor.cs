using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class InventoryEditor
{
    private sealed record FormDraft(Item Item, decimal Price, decimal Cost, string? Version);
    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (RestoreDraft<FormDraft>() is { } d) { draft = d.Item; price = d.Price; cost = d.Cost; version = d.Version; loaded = Id; restored = true; }
    }
    private bool restored;

    [Parameter] public Guid? Id { get; set; }
    private Item draft = new() { Id = Guid.NewGuid(), IsActive = true };
    private decimal price, cost;
    private bool priceValid, costValid = true, minimumValid = true;
    private bool MoneyValid => priceValid && costValid;
    private bool InputsValid => MoneyValid && minimumValid;
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
        if (restored || loaded == Id) return;
        loaded = Id;
        draft = LocalData.Clone(Store.Data.Items.Find(x => x.Id == Id) ?? new Item { Id = Guid.NewGuid(), IsActive = true });
        version = draft.Version is { } value ? Convert.ToBase64String(value) : null;
        price = draft.SellingPriceCentavos / 100m;
        cost = draft.UnitCostCentavos / 100m;
    }

    private Task Save() => Run(async () =>
    {
        Rules.Require(InputsValid, "Correct the highlighted number fields before saving.");
        draft.SellingPriceCentavos = PriceCentavos;
        draft.UnitCostCentavos = CostCentavos;
        await Store.Execute("saveItem", draft.Id, draft, version);
        ClearDraft();
    }, "/items", "Item saved successfully.");
    public override void Dispose() { KeepDraft(new FormDraft(draft, price, cost, version)); base.Dispose(); }
}
