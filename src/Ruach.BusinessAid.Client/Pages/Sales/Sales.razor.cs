using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class Sales
{
    private sealed record FormDraft(Dictionary<Guid, long> Quantities, DateTime? Date, string Search);
    private Dictionary<Guid, long> quantities = [];
    private readonly HashSet<Guid> invalidQuantities = [];
    private DateTime? date = Today().ToDateTime(TimeOnly.MinValue);
    private string search = "";
    private bool showTotals;
    private string message = "";
    private ElementReference cartElement;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (RestoreDraft<FormDraft>() is { } draft)
        {
            quantities = draft.Quantities;
            date = draft.Date;
            search = draft.Search;
        }
    }

    private bool Matches(Item item) => item.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    private IEnumerable<Item> CartItems => Store.Items.Where(x => quantities.ContainsKey(x.Id));
    private IEnumerable<Guid> UnavailableIds => quantities.Keys.Except(Store.Items.Select(x => x.Id));
    private bool TooManyLines => quantities.Values.Count(q => q > 0) > Rules.MaxSaleLines;
    private bool InputsValid => invalidQuantities.Count == 0 && !TooManyLines && !UnavailableIds.Any();
    private bool HasNegativeStock => CartItems.Any(x => quantities[x.Id] > 0 && quantities[x.Id] > x.CurrentQuantity && QuantityValid(x.Id));
    private long TotalItems => quantities.Values.Sum();
    private long Revenue => CartItems.Sum(x => checked(quantities[x.Id] * x.SellingPriceCentavos));
    private long Cost => CartItems.Sum(x => checked(quantities[x.Id] * x.UnitCostCentavos));
    private bool QuantityValid(Guid id) => !invalidQuantities.Contains(id);

    private void SetQuantityValidity(Guid id, bool valid)
    {
        if (valid) invalidQuantities.Remove(id);
        else invalidQuantities.Add(id);
    }

    private void Change(Guid id, int delta)
    {
        if (Saving || !QuantityValid(id)) return;
        message = "";
        var next = Math.Clamp(quantities.GetValueOrDefault(id) + delta, 0, Rules.MaxQuantity);
        if (next == 0) Remove(id);
        else quantities[id] = next;
    }

    private void Remove(Guid id)
    {
        quantities.Remove(id);
        invalidQuantities.Remove(id);
        NumberDrafts.Remove($"sale-quantity-{id}");
    }

    private async Task FocusCart() => await NotificationJS.InvokeVoidAsync("mashalUi.revealCart", cartElement);

    private Task Save() => Run(async () =>
    {
        message = "";
        Rules.Require(!UnavailableIds.Any(), "Remove unavailable items before recording this sale.");
        Rules.Require(InputsValid, "Correct the highlighted quantities before saving.");
        Rules.Require(date.HasValue, "Choose a sale date.");
        var lines = CartItems.Where(x => quantities[x.Id] > 0)
            .Select(x => new SaleLineInput(x.Id, quantities[x.Id])).ToList();
        await Store.Execute("recordSale", Guid.NewGuid(), new SaleInput(lines, date!.Value.ToString("yyyy-MM-dd")));
        ClearDraft();
        quantities.Clear();
        invalidQuantities.Clear();
        search = "";
        showTotals = false;
        message = "Sale saved successfully.";
    });

    public override void Dispose()
    {
        KeepDraft(new FormDraft(quantities, date, search));
        base.Dispose();
    }
}
