using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class InventoryEditor
{
    private string name = "", unit = "pc"; private decimal minimum;
    private Task Save() => Run(async () => { var item = new InventoryItem { Id = Guid.NewGuid(), Name = name, BaseUnit = unit, UnitKind = unit == "g" ? "mass" : unit == "ml" ? "volume" : unit == "pc" ? "count" : "custom", MinimumQuantity = minimum, IsActive = true }; await Store.Execute("saveInventoryItem", item.Id, item); }, "/inventory");

}
