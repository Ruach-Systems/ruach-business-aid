using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class Sales
{
    private sealed record FormDraft(Dictionary<Guid,long> Quantities, DateTime Date, string Search);
    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (RestoreDraft<FormDraft>() is { } d) { quantities = d.Quantities; date = d.Date; search = d.Search; }
    }

    private Dictionary<Guid,long> quantities = [];
    private readonly HashSet<Guid> invalidQuantities = [];
    private bool QuantityValid(Guid id) => !invalidQuantities.Contains(id);
    private void SetQuantityValidity(Guid id, bool valid) { if (valid) invalidQuantities.Remove(id); else invalidQuantities.Add(id); }
    private bool TooManyLines => Store.Items.Count(x => quantities.GetValueOrDefault(x.Id) > 0) > Rules.MaxSaleLines;
    private bool InputsValid => invalidQuantities.Count == 0 && !TooManyLines;
    private DateTime date = Today().ToDateTime(TimeOnly.MinValue);
    private bool showTotals;
    private string message = "";
    private string search = "";
    private bool Matches(Item item) => item.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    private IEnumerable<Item> VisibleItems => Store.Items.Where(x => Matches(x) || quantities.GetValueOrDefault(x.Id) > 0 || !QuantityValid(x.Id));
    private int MatchCount => Store.Items.Count(Matches);
    private int SelectedHiddenByName => Store.Items.Count(x => !Matches(x) && quantities.GetValueOrDefault(x.Id) > 0);
    private void ClearSearch() => search = "";
    private void Ensure(Guid id) => quantities.TryAdd(id, 0);
    private void Change(Guid id, int amount) { if (QuantityValid(id)) quantities[id] = Math.Clamp(quantities.GetValueOrDefault(id) + amount, 0, Rules.MaxQuantity); }
    private long TotalItems => quantities.Values.Sum();
    private long Revenue => !InputsValid ? 0 : Store.Items.Sum(x => checked(quantities.GetValueOrDefault(x.Id) * x.SellingPriceCentavos));
    private long Cost => !InputsValid ? 0 : Store.Items.Sum(x => checked(quantities.GetValueOrDefault(x.Id) * x.UnitCostCentavos));
    private bool HasNegativeResult => InputsValid && Store.Items.Any(x => quantities.GetValueOrDefault(x.Id) > 0 && quantities.GetValueOrDefault(x.Id) > x.CurrentQuantity);
    private Task Save() => Run(async () =>
    {
        message = "";
        Rules.Require(InputsValid, "Correct the highlighted quantities before saving.");
        var lines = Store.Items.Where(x => quantities.GetValueOrDefault(x.Id) > 0)
            .Select(x => new SaleLineInput(x.Id, quantities[x.Id])).ToList();
        await Store.Execute("recordSale", Guid.NewGuid(), new SaleInput(lines, date.ToString("yyyy-MM-dd")));
        ClearDraft();
        quantities.Clear();
        invalidQuantities.Clear();
        search = "";
        message = "Sale saved successfully.";
    });
    public override void Dispose() { KeepDraft(new FormDraft(quantities, date, search)); base.Dispose(); }
}
