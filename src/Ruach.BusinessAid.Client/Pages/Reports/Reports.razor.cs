using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class Reports
{
    private string period = "month";
    private DateTime from, to, appliedFrom, appliedTo;
    private int requestRevision;
    private async Task ChangePeriod() { SetPeriod(); await Load(); }
    private FinancialSummary? summary;
    private List<SalesTrend> trends = [];
    private List<ItemReport> items = [];
    private List<ExpenseReport> expenses = [];
    private bool CustomRange => period == "custom";
    private string TodayText => Today().ToString("yyyy-MM-dd");

    protected override async Task OnInitializedAsync() { SetPeriod(); await Load(); }

    private void SetPeriod()
    {
        if (CustomRange)
            return;

        var range = Period(period);
        from = DateTime.Parse(range.From);
        to = DateTime.Parse(range.To);
    }

    private async Task Load()
    {
        var revision = ++requestRevision;
        Error = ""; summary = null; Saving = false;
        if (from.Date < new DateTime(2026, 1, 1) || from.Date > to.Date || DateOnly.FromDateTime(to) > Today())
        {
            Error = "Choose a valid date range ending today or earlier.";
            return;
        }

        Saving = true;
        try
        {
            summary = null;
            if (Store.User?.IsDemo == true) { LoadLocal(); return; }
            if (!Store.Online || Store.Data.Business is null) return;
            var id = Store.Data.Business.Id;
            var requestedFrom = from;
            var requestedTo = to;
            var start = requestedFrom.ToString("yyyy-MM-dd");
            var end = requestedTo.ToString("yyyy-MM-dd");
            var summaryTask = Api.Report<FinancialSummary>("summary", id, start, end);
            var trendTask = Api.Report<List<SalesTrend>>("sales-trend", id, start, end, "day");
            var itemTask = Api.Report<List<ItemReport>>("items", id, start, end);
            var expenseTask = Api.Report<List<ExpenseReport>>("expenses-by-category", id, start, end);
            await Task.WhenAll(summaryTask, trendTask, itemTask, expenseTask);
            if (revision != requestRevision) return;
            summary = await summaryTask;
            trends = await trendTask;
            items = await itemTask;
            expenses = await expenseTask;
            appliedFrom = requestedFrom; appliedTo = requestedTo;
        }
        catch (Exception error) { if (revision == requestRevision) Error = error.Message; }
        finally { if (revision == requestRevision) Saving = false; }
    }
    private void LoadLocal()
    {
        var start = from.ToString("yyyy-MM-dd"); var end = to.ToString("yyyy-MM-dd");
        bool Within(string date) => string.CompareOrdinal(date, start) >= 0 && string.CompareOrdinal(date, end) <= 0;
        var sales = Store.Sales.Where(x => Within(x.SaleDate)).ToList();
        var costs = Store.Expenses.Where(x => Within(x.ExpenseDate)).ToList();
        var revenue = sales.Sum(x => x.TotalRevenueCentavos);
        var cost = sales.Sum(x => x.TotalCostCentavos);
        var expense = costs.Sum(x => x.AmountCentavos);
        summary = new(revenue, cost, expense, revenue - cost, revenue - cost - expense, sales.Count, sales.Sum(x => x.TotalItems));
        trends = sales.Select(x => x.SaleDate).Concat(costs.Select(x => x.ExpenseDate)).Distinct().OrderBy(x => x).Select(date => {
            var daily = sales.Where(x => x.SaleDate == date).ToList();
            var r = daily.Sum(x => x.TotalRevenueCentavos); var c = daily.Sum(x => x.TotalCostCentavos);
            var e = costs.Where(x => x.ExpenseDate == date).Sum(x => x.AmountCentavos);
            return new SalesTrend(date, r, c, r-c, e, r-c-e);
        }).ToList();
        items = sales.SelectMany(x => x.Lines).GroupBy(x => x.ItemId).Select(g => new ItemReport(g.Key, g.First().ItemName, g.Sum(x => x.Quantity), g.Sum(x => x.LineRevenueCentavos), g.Sum(x => x.LineCostCentavos), g.Sum(x => x.LineRevenueCentavos-x.LineCostCentavos))).OrderByDescending(x => x.RevenueCentavos).ToList();
        expenses = costs.GroupBy(x => x.Category).Select(g => new ExpenseReport(g.Key, g.Sum(x => x.AmountCentavos), expense == 0 ? 0 : g.Sum(x => x.AmountCentavos)*100m/expense)).ToList();
        appliedFrom = from; appliedTo = to;
    }
}
