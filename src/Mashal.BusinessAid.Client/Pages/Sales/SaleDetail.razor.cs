using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class SaleDetail
{
    [Parameter] public Guid Id { get; set; }
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private Guid? scrolledSaleId;

    private Sale? CurrentSale => Store.Sales.FirstOrDefault(x => x.Id == Id);
    private IEnumerable<Sale> DaySales => CurrentSale is { } sale
        ? Store.Sales.Where(x => x.SaleDate == sale.SaleDate)
        : [];
    private long DayRevenue => DaySales.Sum(x => x.TotalRevenueCentavos);
    private long DayGrossProfit => DaySales.Sum(x => x.TotalProfitCentavos);
    private long DayExpenses => CurrentSale is { } sale
        ? Store.Expenses.Where(x => x.ExpenseDate == sale.SaleDate).Sum(x => x.AmountCentavos)
        : 0;
    private long DayNetProfit => DayGrossProfit - DayExpenses;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (scrolledSaleId == Id)
            return;

        scrolledSaleId = Id;
        await JS.InvokeVoidAsync("mashalUi.scrollAppToTop");
    }

    private static string HeadingDescription(Sale sale)
    {
        var parts = new List<string> { Calculations.FriendlyDate(sale.SaleDate, Calculations.Today()) };
        if (!string.IsNullOrWhiteSpace(sale.Location))
            parts.Add(sale.Location);
        parts.Add($"{Calculations.Quantity(sale.TotalItems)} items");
        return string.Join(" · ", parts);
    }
}
