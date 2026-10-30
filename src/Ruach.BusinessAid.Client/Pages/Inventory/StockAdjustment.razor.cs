using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Pages;

public partial class StockAdjustment
{
    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId;
    private long count;
    private bool countValid = true;
    private string reason = "Stock count correction", note = "";
    private string? version;
    protected override void OnParametersSet() { if (SelectedItem is { } id) { itemId = id; Choose(); } }
    private void Choose()
    {
        var item = Store.AllItems.FirstOrDefault(x => x.Id == itemId);
        count = Math.Max(0, item?.CurrentQuantity ?? 0);
        version = item?.Version is { } value ? Convert.ToBase64String(value) : null;
    }
    private Task Save() => Run(async () => { Rules.Require(countValid, "Correct the counted quantity before saving."); await Store.Execute("adjustStock", Guid.NewGuid(), new AdjustmentInput(itemId, count, reason, note), version); }, "/items", "Stock corrected successfully.");
}
