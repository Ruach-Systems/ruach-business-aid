using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class StockAdjustment
{
    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId; private decimal count; private string reason = "Stock count correction", note = ""; private string? version;
    protected override void OnParametersSet() { if (SelectedItem is { } id) { itemId = id; Choose(); } }
    private void Choose() { var item = Store.Inventory.FirstOrDefault(x => x.Id == itemId); count = item?.CurrentQuantity ?? 0; version = item?.Version is { } v ? Convert.ToBase64String(v) : null; }
    private Task Save() => Run(() => Store.Execute("adjustStock", Guid.NewGuid(), new AdjustmentInput(itemId, count, reason, note), version), "/inventory");

}
