using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class InventoryDetail
{
    [Parameter] public Guid Id { get; set; }
    private InventoryItem? Item => Store.Data.InventoryItems.Find(x => x.Id == Id);
}
