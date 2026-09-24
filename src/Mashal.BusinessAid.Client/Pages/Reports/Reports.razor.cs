using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Reports
{
    private string period = "month";
    private DateTime from, to;
    private FinancialSummary? summary;
    private List<SalesTrend> trends = [];
    private List<ItemReport> items = [];
    private List<ExpenseReport> expenses = [];
    protected override async Task OnInitializedAsync() { SetPeriod(); await Load(); }
    private void SetPeriod() { var range = Period(period); from = DateTime.Parse(range.From); to = DateTime.Parse(range.To); }
    private Task Load() => Run(async () =>
    {
        summary = null;
        if (!Store.Online || Store.User?.IsDemo == true || Store.Data.Business is null) return;
        var id = Store.Data.Business.Id;
        var start = from.ToString("yyyy-MM-dd");
        var end = to.ToString("yyyy-MM-dd");
        var summaryTask = Api.Report<FinancialSummary>("summary", id, start, end);
        var trendTask = Api.Report<List<SalesTrend>>("sales-trend", id, start, end, "day");
        var itemTask = Api.Report<List<ItemReport>>("items", id, start, end);
        var expenseTask = Api.Report<List<ExpenseReport>>("expenses-by-category", id, start, end);
        await Task.WhenAll(summaryTask, trendTask, itemTask, expenseTask);
        summary = await summaryTask;
        trends = await trendTask;
        items = await itemTask;
        expenses = await expenseTask;
    });
}
