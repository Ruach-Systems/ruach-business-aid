using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Pages;

public partial class StockReceipt
{
    private sealed record FormDraft(Guid Item, long Quantity, string Note);
    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (RestoreDraft<FormDraft>() is { } d) { itemId = d.Item; quantity = d.Quantity; note = d.Note; restored = true; }
    }
    private bool restored;

    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId;
    private long quantity = 1;
    private bool quantityValid = true;
    private string note = "";
    private Item? Item => Store.AllItems.FirstOrDefault(x => x.Id == itemId);
    protected override void OnParametersSet() { if (restored) return; if (SelectedItem is { } id) itemId = id; }
    private Task Save() => Run(async () =>
    {
        Rules.Require(quantityValid, "Correct the quantity before saving.");
        var item = Item ?? throw new InvalidOperationException("Select an item.");
        await Store.Execute("receiveStock", Guid.NewGuid(), new ReceiptInput(item.Id, quantity, note));
        ClearDraft();
    }, "/items", "Stock received successfully.");
    public override void Dispose() { KeepDraft(new FormDraft(itemId, quantity, note)); base.Dispose(); }
}
