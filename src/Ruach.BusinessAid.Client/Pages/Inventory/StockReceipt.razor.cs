using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Pages;

public partial class StockReceipt
{
    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId;
    private long quantity = 1;
    private string note = "";
    private Item? Item => Store.AllItems.FirstOrDefault(x => x.Id == itemId);
    protected override void OnParametersSet() { if (SelectedItem is { } id) itemId = id; }
    private Task Save() => Run(async () =>
    {
        var item = Item ?? throw new InvalidOperationException("Select an item.");
        await Store.Execute("receiveStock", Guid.NewGuid(), new ReceiptInput(item.Id, quantity, note));
    }, "/items");
}
