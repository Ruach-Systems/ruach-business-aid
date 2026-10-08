using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Pages;

public partial class InventoryDetail
{
    [Parameter] public Guid Id { get; set; }
    private Item? Item => Store.Data.Items.Find(x => x.Id == Id && x.DeletedAt is null);
    private static string MarkupText(long price, long unitCost)
    {
        var markup = Calculations.Markup(price, unitCost);
        return markup is null ? "markup not applicable" : $"{markup:0.0}% markup";
    }
}
