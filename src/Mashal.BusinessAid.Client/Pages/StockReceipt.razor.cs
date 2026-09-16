using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class StockReceipt
{
    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId; private decimal quantity = 1, cost; private string unit = "pc", note = "";
    private InventoryItem? Item => Store.Inventory.FirstOrDefault(x => x.Id == itemId);
    private IEnumerable<string> Units => Item?.BaseUnit switch { "g" => new[] { "g", "kg" }, "ml" => new[] { "ml", "L" }, _ => new[] { Item?.BaseUnit ?? "pc" } };
    protected override void OnParametersSet() { if (SelectedItem is { } id) { itemId = id; ChooseItem(); } }
    private void ChooseItem() => unit = Item?.BaseUnit ?? "pc";
    private Task Save() => Run(async () => { var item = Item ?? throw new InvalidOperationException("Select an inventory item."); await Store.Execute("receiveStock", Guid.NewGuid(), new ReceiptInput(item.Id, quantity, unit, Conversion(unit, item.BaseUnit), Centavos(cost), note)); }, "/inventory");

}
