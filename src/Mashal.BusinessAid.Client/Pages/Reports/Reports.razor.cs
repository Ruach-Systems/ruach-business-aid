using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Reports
{
    private string period = "month", grouping = "day"; private DateTime from, to; private FinancialSummary? summary; private List<SalesTrend> trends = []; private List<ProductReport> products = []; private List<ExpenseReport> expenses = [];
    protected override async Task OnInitializedAsync() { SetPeriod(); await Load(); }
    private void SetPeriod() { if (period == "custom") return; var range = Period(period); from = DateTime.Parse(range.From); to = DateTime.Parse(range.To); }
    private Task Load() => Run(async () => { summary = null; if (!Store.Online || Store.User?.IsDemo == true || Store.Data.Business is null) return; Rules.Require(from <= to, "Choose a valid inclusive date range."); var id = Store.Data.Business.Id; var a = from.ToString("yyyy-MM-dd"); var b = to.ToString("yyyy-MM-dd"); var s = Api.Report<FinancialSummary>("summary", id, a, b); var t = Api.Report<List<SalesTrend>>("sales-trend", id, a, b, grouping); var p = Api.Report<List<ProductReport>>("products", id, a, b); var e = Api.Report<List<ExpenseReport>>("expenses-by-category", id, a, b); await Task.WhenAll(s, t, p, e); summary = await s; trends = await t; products = await p; expenses = await e; });

}
