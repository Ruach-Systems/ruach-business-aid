using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Inventory
{
    private string search = "";
    private IEnumerable<Item> Items => Store.AllItems.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
}
