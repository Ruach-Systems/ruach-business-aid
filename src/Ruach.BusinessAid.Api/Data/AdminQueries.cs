using Dapper;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Api.Data;

// Separate read-only surface: never grant platform admins workspace membership or sync access.
public sealed class AdminQueries(SqlConnectionFactory connections, AccountService accounts)
{
    private static void Range(string from, string to) => Rules.Require(
        DateOnly.TryParseExact(from, "yyyy-MM-dd", out var start) && DateOnly.TryParseExact(to, "yyyy-MM-dd", out var end) && start <= end,
        "Choose a valid inclusive date range.");

    public async Task<AdminDirectory> Directory(Guid actor, string kind, string search, string from, string to, int page = 1, int pageSize = 25)
    {
        await accounts.RequireAdmin(actor); Range(from,to);
        Rules.Require(kind is "users" or "businesses", "Unknown directory.");
        Rules.Require(search.Length <= 200 && page is >= 1 and <= 1000000 && pageSize is >= 1 and <= 100, "Choose a valid search and page size (1–100).");
        await using var c=await connections.Open();
        var p=new { search, from, to, skip=(page-1)*pageSize,pageSize };
        var result=new AdminDirectory();
        if(kind=="users")
        {
            const string where="FROM dbo.Users u WHERE @search='' OR CHARINDEX(@search,u.DisplayName)>0 OR CHARINDEX(@search,u.Email)>0 OR CHARINDEX(@search,u.PhoneNumber)>0";
            result.Total=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) "+where,p);
            result.Users=(await c.QueryAsync<AdminUser>("SELECT u.Id Uid,u.DisplayName,u.Email,u.PhoneNumber,u.CreatedAt,(SELECT COUNT(*) FROM dbo.Businesses b WHERE b.OwnerUid=u.Id) BusinessCount "+where+" ORDER BY u.CreatedAt DESC,u.Id OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY",p)).ToList();
        }
        else
        {
            const string where="FROM dbo.Businesses b JOIN dbo.Users u ON u.Id=b.OwnerUid WHERE @search='' OR CHARINDEX(@search,b.Name)>0 OR CHARINDEX(@search,b.DefaultLocation)>0 OR CHARINDEX(@search,u.Email)>0 OR CHARINDEX(@search,u.DisplayName)>0";
            result.Total=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) "+where,p);
            result.Businesses=(await c.QueryAsync<Business>("SELECT b.* "+where+" ORDER BY b.CreatedAt DESC,b.Id OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY",p)).ToList();
            if(result.Businesses.Count>0) result.Totals=(await c.QueryAsync<AdminBusinessTotal>("""
                SELECT b.Id,b.Name,COALESCE(s.Revenue,0) RevenueCentavos,COALESCE(s.Cost,0) CostCentavos,
                COALESCE(e.Expense,0) ExpenseCentavos,COALESCE(s.Revenue-s.Cost,0)-COALESCE(e.Expense,0) ProfitCentavos
                FROM dbo.Businesses b
                OUTER APPLY (SELECT SUM(TotalRevenueCentavos) Revenue,SUM(TotalCostCentavos) Cost FROM dbo.ItemSales WHERE BusinessId=b.Id AND DeletedAt IS NULL AND SaleDate BETWEEN @from AND @to) s
                OUTER APPLY (SELECT SUM(AmountCentavos) Expense FROM dbo.BusinessExpenses WHERE BusinessId=b.Id AND DeletedAt IS NULL AND ExpenseDate BETWEEN @from AND @to) e
                WHERE b.Id IN @ids
                """,new{from,to,ids=result.Businesses.Select(x=>x.Id)})).ToList();
        }
        return result;
    }

    public async Task<AdminAnalytics> Analytics(Guid actor, string from, string to, Guid? business = null, Guid? owner = null)
    {
        await accounts.RequireAdmin(actor); Range(from, to);
        await using var c = await connections.Open();
        if (business.HasValue && !await c.ExecuteScalarAsync<bool>("SELECT COUNT(*) FROM dbo.Businesses WHERE Id=@business", new { business }))
            throw new DomainException("not_found", "Business not found.", 404);
        if (owner.HasValue && !await c.ExecuteScalarAsync<bool>("SELECT COUNT(*) FROM dbo.Users WHERE Id=@owner", new { owner }))
            throw new DomainException("not_found", "User not found.", 404);
        // UNION avoids multiplying sales by expenses. Owner scope is owned businesses, not actor attribution.
        const string activity = """
            WITH Scope AS (SELECT Id,Name FROM dbo.Businesses WHERE (@business IS NULL OR Id=@business) AND (@owner IS NULL OR OwnerUid=@owner)),
            Activity AS (
              SELECT s.BusinessId,s.SaleDate Day,s.TotalRevenueCentavos Revenue,s.TotalCostCentavos Cost,CAST(0 AS bigint) Expense,s.TotalItems Items,1 Transactions
              FROM dbo.ItemSales s JOIN Scope b ON b.Id=s.BusinessId WHERE s.DeletedAt IS NULL AND s.SaleDate BETWEEN @from AND @to
              UNION ALL
              SELECT e.BusinessId,e.ExpenseDate,0,0,e.AmountCentavos,0,0
              FROM dbo.BusinessExpenses e JOIN Scope b ON b.Id=e.BusinessId WHERE e.DeletedAt IS NULL AND e.ExpenseDate BETWEEN @from AND @to
            )
            """;
        var p = new { from, to, business, owner };
        var summary = await c.QuerySingleAsync<FinancialSummary>(activity + """
            SELECT COALESCE(SUM(Revenue),0) RevenueCentavos,COALESCE(SUM(Cost),0) CostCentavos,COALESCE(SUM(Expense),0) ExpenseCentavos,
            COALESCE(SUM(Revenue-Cost),0) GrossProfitCentavos,COALESCE(SUM(Revenue-Cost-Expense),0) ProfitCentavos,
            COALESCE(SUM(Transactions),0) Transactions,COALESCE(SUM(Items),0) ItemCount FROM Activity
            """, p);
        var trend = (await c.QueryAsync<SalesTrend>(activity + """
            SELECT CONVERT(varchar(10),Day,23) Period,SUM(Revenue) RevenueCentavos,SUM(Cost) CostCentavos,
            SUM(Revenue-Cost) GrossProfitCentavos,SUM(Expense) ExpenseCentavos,SUM(Revenue-Cost-Expense) ProfitCentavos
            FROM Activity GROUP BY Day ORDER BY Day
            """, p)).ToList();
        var top = (await c.QueryAsync<AdminBusinessTotal>(activity + """
            SELECT TOP(5) b.Id,b.Name,COALESCE(SUM(a.Revenue),0) RevenueCentavos,COALESCE(SUM(a.Cost),0) CostCentavos,
            COALESCE(SUM(a.Expense),0) ExpenseCentavos,COALESCE(SUM(a.Revenue-a.Cost-a.Expense),0) ProfitCentavos
            FROM Scope b LEFT JOIN Activity a ON a.BusinessId=b.Id GROUP BY b.Id,b.Name ORDER BY RevenueCentavos DESC,b.Name,b.Id
            """, p)).ToList();
        return new(summary, trend, top);
    }

    public async Task<List<AdminMember>> Members(Guid actor, Guid business)
    {
        await accounts.RequireAdmin(actor); await using var c = await connections.Open();
        return (await c.QueryAsync<AdminMember>("SELECT u.Id Uid,u.DisplayName,u.Email,m.Role FROM dbo.BusinessMembers m JOIN dbo.Users u ON u.Id=m.UserId WHERE m.BusinessId=@business ORDER BY u.DisplayName,u.Id", new { business })).ToList();
    }

    public async Task<List<AdminMembership>> Memberships(Guid actor, Guid user)
    {
        await accounts.RequireAdmin(actor); await using var c = await connections.Open();
        return (await c.QueryAsync<AdminMembership>("SELECT b.Id BusinessId,b.Name,m.Role FROM dbo.BusinessMembers m JOIN dbo.Businesses b ON b.Id=m.BusinessId WHERE m.UserId=@user ORDER BY b.Name,b.Id", new { user })).ToList();
    }

    public async Task<AdminRecords> Records(Guid actor, Guid business, string kind, string from, string to, int page = 1, int pageSize = 25)
    {
        await accounts.RequireAdmin(actor); Range(from, to);
        Rules.Require(page is >= 1 and <= 1000000 && pageSize is >= 1 and <= 100, "Choose a valid page and page size (1–100).");
        var (table, date, columns) = kind switch
        {
            "sales" => ("ItemSales", "SaleDate", "Id,BusinessId,CreatedAt,UpdatedAt,Location,TotalItems,TotalRevenueCentavos,TotalCostCentavos,TotalProfitCentavos,CONVERT(varchar(10),SaleDate,23) SaleDate"),
            "expenses" => ("BusinessExpenses", "ExpenseDate", "Id,BusinessId,CreatedAt,UpdatedAt,Description,Category,AmountCentavos,CONVERT(varchar(10),ExpenseDate,23) ExpenseDate"),
            "items" => ("Items", "", "*"),
            "receipts" => ("StockEntries", "CONVERT(date,SWITCHOFFSET(ReceivedAt,'+08:00'))", "*"),
            "adjustments" => ("StockMovements", "CONVERT(date,SWITCHOFFSET(CreatedAt,'+08:00'))", "*"),
            _ => throw new DomainException("not_found", "Records not found.", 404)
        };
        await using var c = await connections.Open();
        var data = new AppData { Business = await c.QuerySingleOrDefaultAsync<Business>("SELECT * FROM dbo.Businesses WHERE Id=@business", new { business })
            ?? throw new DomainException("not_found", "Business not found.", 404) };
        var where = "BusinessId=@business AND DeletedAt IS NULL" + (date.Length > 0 ? $" AND {date} BETWEEN @from AND @to" : "") + (kind == "adjustments" ? " AND ReferenceType='adjustment'" : "");
        var p = new { business, from, to, skip = (page - 1) * pageSize, pageSize };
        var total = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{table} WHERE {where}", p);
        var sql = $"SELECT {columns} FROM dbo.{table} WHERE {where} ORDER BY {(date.Length > 0 ? date + " DESC," : "")}CreatedAt DESC,Id OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
        switch (kind)
        {
            case "sales":
                data.Sales = (await c.QueryAsync<Sale>(sql, p)).ToList();
                if (data.Sales.Count > 0)
                {
                    var lines = (await c.QueryAsync<Line>("SELECT * FROM dbo.ItemSaleLines WHERE BusinessId=@business AND SaleId IN @ids ORDER BY ItemName,Id", new { business, ids = data.Sales.Select(x => x.Id) })).ToLookup(x => x.SaleId);
                    foreach (var sale in data.Sales) sale.Lines = lines[sale.Id].Cast<SaleLine>().ToList();
                }
                break;
            case "expenses": data.Expenses = (await c.QueryAsync<Expense>(sql, p)).ToList(); break;
            case "items": data.Items = (await c.QueryAsync<Item>(sql, p)).ToList(); break;
            case "receipts": data.Receipts = (await c.QueryAsync<StockReceipt>(sql, p)).ToList(); break;
            case "adjustments": data.Movements = (await c.QueryAsync<StockMovement>(sql, p)).ToList(); break;
        }
        return new(data, total, page, pageSize);
    }
    public async Task<object> Report(Guid actor, Guid business, string report, string from, string to)
    {
        await accounts.RequireAdmin(actor); Range(from, to);
        await using var c = await connections.Open();
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Businesses WHERE Id=@business", new { business }) == 0)
            throw new DomainException("not_found", "Business not found.", 404);
        return await ReportQueries.Read(c, business, report, from, to);
    }
    private sealed class Line : SaleLine { public Guid SaleId { get; set; } }
}
