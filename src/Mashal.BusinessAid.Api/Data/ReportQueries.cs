using Dapper;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Api.Data;

public sealed class ReportQueries(SqlConnectionFactory connections)
{
    public async Task<object> Query(Guid user, Guid business, string report, string from, string to, string grouping = "day")
    {
        Rules.Require(DateOnly.TryParseExact(from, "yyyy-MM-dd", out var start) && DateOnly.TryParseExact(to, "yyyy-MM-dd", out var end) && start <= end, "Choose a valid inclusive date range.");
        Rules.Require(grouping is "day" or "week" or "month", "Invalid trend grouping.");
        await using var c = await connections.Open();
        await BusinessService.Authorize(c, null, user, business, management: true);
        var p = new { business, from, to };
        if (report == "summary") return await c.QuerySingleAsync<FinancialSummary>(
            """
            SELECT COALESCE(s.RevenueCentavos,0) RevenueCentavos,COALESCE(s.CostCentavos,0) CostCentavos,
             COALESCE(e.ExpenseCentavos,0) ExpenseCentavos,COALESCE(s.RevenueCentavos-s.CostCentavos,0) GrossProfitCentavos,
             COALESCE(s.RevenueCentavos-s.CostCentavos,0)-COALESCE(e.ExpenseCentavos,0) ProfitCentavos,
             COALESCE(s.Transactions,0) Transactions,COALESCE(s.ItemCount,0) ItemCount
            FROM (SELECT SUM(TotalRevenueCentavos) RevenueCentavos,SUM(TotalCostCentavos) CostCentavos,COUNT(*) Transactions,SUM(TotalItems) ItemCount
             FROM dbo.ItemSales WHERE BusinessId=@business AND SaleDate BETWEEN @from AND @to AND DeletedAt IS NULL) s
            CROSS JOIN (SELECT SUM(AmountCentavos) ExpenseCentavos FROM dbo.BusinessExpenses
             WHERE BusinessId=@business AND ExpenseDate BETWEEN @from AND @to AND DeletedAt IS NULL) e
            """, p);
        if (report == "items") return (await c.QueryAsync<ItemReport>(
            """
            SELECT l.ItemId,MAX(l.ItemName) ItemName,SUM(l.Quantity) Quantity,
             SUM(l.LineRevenueCentavos) RevenueCentavos,SUM(l.LineCostCentavos) CostCentavos,
             SUM(l.LineRevenueCentavos-l.LineCostCentavos) ProfitCentavos
            FROM dbo.ItemSaleLines l JOIN dbo.ItemSales s ON s.BusinessId=l.BusinessId AND s.Id=l.SaleId
            WHERE s.BusinessId=@business AND s.SaleDate BETWEEN @from AND @to AND s.DeletedAt IS NULL
            GROUP BY l.ItemId ORDER BY RevenueCentavos DESC
            """, p)).ToList();
        if (report == "expenses-by-category") return (await c.QueryAsync<ExpenseReport>(
            """
            SELECT Category,SUM(AmountCentavos) AmountCentavos,
             CAST(100.0*SUM(AmountCentavos)/NULLIF(SUM(SUM(AmountCentavos)) OVER(),0) AS decimal(9,2)) Percentage
            FROM dbo.BusinessExpenses WHERE BusinessId=@business AND ExpenseDate BETWEEN @from AND @to AND DeletedAt IS NULL
            GROUP BY Category ORDER BY AmountCentavos DESC
            """, p)).ToList();
        if (report == "sales-trend")
        {
            var bucket = grouping switch { "month" => "DATEFROMPARTS(YEAR(SaleDate),MONTH(SaleDate),1)", "week" => "DATEADD(day,-(DATEDIFF(day,'19000101',SaleDate)%7),SaleDate)", _ => "SaleDate" };
            return (await c.QueryAsync<SalesTrend>($"""
                SELECT CONVERT(varchar(10),{bucket},23) Period,SUM(TotalRevenueCentavos) RevenueCentavos,
                 SUM(TotalCostCentavos) CostCentavos,SUM(TotalRevenueCentavos-TotalCostCentavos) GrossProfitCentavos
                FROM dbo.ItemSales WHERE BusinessId=@business AND SaleDate BETWEEN @from AND @to AND DeletedAt IS NULL
                GROUP BY {bucket} ORDER BY Period
                """, p)).ToList();
        }
        throw new DomainException("not_found", "Report not found.", 404);
    }
}
