using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Inventory
{
    private string search = ""; private IEnumerable<InventoryItem> Items => Store.Inventory.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
}
