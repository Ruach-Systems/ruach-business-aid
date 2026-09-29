using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Dashboard
{
    private IEnumerable<Sale> TodaySales => Store.Sales.Where(x => x.SaleDate == Today().ToString("yyyy-MM-dd"));
    private long Revenue => TodaySales.Sum(x => x.TotalRevenueCentavos);
    private long Cost => TodaySales.Sum(x => x.TotalCostCentavos);
    private IEnumerable<Expense> TodayExpenses => Store.Expenses.Where(x => x.ExpenseDate == Today().ToString("yyyy-MM-dd"));
    private long ExpenseTotal => TodayExpenses.Sum(x => x.AmountCentavos);
    private IEnumerable<Item> LowStock => Store.Items.Where(x => x.CurrentQuantity <= x.MinimumQuantity);
    private static string SaleTitle(Sale sale) => string.IsNullOrWhiteSpace(sale.Location)
        ? $"{Quantity(sale.TotalItems)} items"
        : $"{Quantity(sale.TotalItems)} items · {sale.Location}";

}
