using System.Text.Json;
using Dapper;
using Ruach.BusinessAid.Api.Data;
using Ruach.BusinessAid.Shared;
using Xunit;

namespace Ruach.Tests;

[Trait("Category", "Integration")]
public class AdminAnalyticsSqlTests
{
    private readonly SqlConnectionFactory connections = new(Environment.GetEnvironmentVariable("ConnectionStrings__Mashal")!);
    private static Operation Op(string type, Guid id, object value) => new(Guid.NewGuid(), id, type, null, JsonSerializer.SerializeToElement(value, Wire.Json));

    [Fact]
    public async Task AdminReadsAreScopedAccuratePagedAndDoNotGrantWorkspaceAccess()
    {
        var identities = new IdentityRepository(connections, AccountFixture.Configuration);
        var owner = await identities.SignIn(Guid.NewGuid().ToString(), "Owner", "owner@example.invalid", null);
        var other = await identities.SignIn(Guid.NewGuid().ToString(), "Other", "other@example.invalid", null);
        var admin = await identities.SignIn(Guid.NewGuid().ToString(), "Admin", "admin@example.invalid", null, true);
        var unverified = await identities.SignIn(Guid.NewGuid().ToString(), "Unverified", "admin@example.invalid", null, false);
        var a = await AccountFixture.Provision(connections, owner.Uid);
        var b = await AccountFixture.Provision(connections, owner.Uid);
        var excluded = await AccountFixture.Provision(connections, other.Uid);
        var accounts = new AccountService(connections, identities);
        var queries = new AdminQueries(connections, accounts);
        var service = new BusinessService(connections, identities);
        var item = Guid.NewGuid(); var sale = Guid.NewGuid(); var deleted = Guid.NewGuid();
        await service.Push(owner.Uid, new(2,a,[
            Op("saveItem",item,new Item{Name="Snapshot item",SellingPriceCentavos=1000,UnitCostCentavos=600,IsActive=true}),
            Op("recordSale",sale,new SaleInput([new(item,2)],"2026-09-15")),
            Op("recordSale",Guid.NewGuid(),new SaleInput([new(item,1)],"2026-09-15")),
            Op("saveExpense",Guid.NewGuid(),new Expense{Description="Freight",Category="Transport",ExpenseDate="2026-09-15",AmountCentavos=300}),
            Op("saveExpense",Guid.NewGuid(),new Expense{Description="Rent",Category="Rent",ExpenseDate="2026-09-16",AmountCentavos=1500}),
            Op("saveExpense",deleted,new Expense{Description="Deleted",Category="Rent",ExpenseDate="2026-09-16",AmountCentavos=9999}),
            Op("saveExpense",Guid.NewGuid(),new Expense{Description="Outside",Category="Rent",ExpenseDate="2026-09-17",AmountCentavos=9999})]));
        await service.Push(owner.Uid,new(2,b,[Op("saveExpense",Guid.NewGuid(),new Expense{Description="Other owned",Category="Rent",ExpenseDate="2026-09-16",AmountCentavos=200})]));
        await service.Push(other.Uid,new(2,excluded,[Op("saveExpense",Guid.NewGuid(),new Expense{Description="Unrelated",Category="Rent",ExpenseDate="2026-09-16",AmountCentavos=9999})]));
        await using(var c=await connections.Open()) await c.ExecuteAsync("UPDATE dbo.BusinessExpenses SET DeletedAt=SYSUTCDATETIME() WHERE BusinessId=@a AND Id=@deleted",new{a,deleted});
        var result=await queries.Analytics(admin.Uid,"2026-09-15","2026-09-16",a);
        Assert.Equal(new FinancialSummary(3000,1800,1800,1200,-600,2,3),result.Summary);
        Assert.Equal(-1500,result.Trend.Single(x=>x.Period=="2026-09-16").ProfitCentavos);
        Assert.Equal(result.Summary.ProfitCentavos,result.Trend.Sum(x=>x.ProfitCentavos));
        var owned=await queries.Analytics(admin.Uid,"2026-09-15","2026-09-16",owner:owner.Uid);
        Assert.Equal(-800,owned.Summary.ProfitCentavos); Assert.Equal(2,owned.TopBusinesses.Count);
        var first=await queries.Records(admin.Uid,a,"sales","2026-09-15","2026-09-16",1,1);
        var second=await queries.Records(admin.Uid,a,"sales","2026-09-15","2026-09-16",2,1);
        Assert.Equal(2,first.Total); Assert.NotEqual(first.Data.Sales.Single().Id,second.Data.Sales.Single().Id);
        Assert.All(first.Data.Sales.Single().Lines,x=>Assert.Equal("Snapshot item",x.ItemName));
        Assert.Empty((await queries.Records(admin.Uid,b,"sales","2026-09-15","2026-09-16")).Data.Sales);
        Assert.Equal(2,(await queries.Records(admin.Uid,a,"expenses","2026-09-15","2026-09-16")).Total);
        Assert.Single(await queries.Members(admin.Uid,a)); Assert.Equal(2,(await queries.Memberships(admin.Uid,owner.Uid)).Count);
        Assert.Equal(result.Summary,(FinancialSummary)await queries.Report(admin.Uid,a,"summary","2026-09-15","2026-09-16"));
        var directory=await queries.Directory(admin.Uid,"businesses","","2026-09-15","2026-09-16",1,100);
        Assert.Equal(-600,directory.Totals.Single(x=>x.Id==a).ProfitCentavos);
        Assert.Single((await queries.Directory(admin.Uid,"users","","2026-09-15","2026-09-16",1,1)).Users);
        Assert.Empty((await queries.Directory(admin.Uid,"businesses",Guid.NewGuid().ToString(),"2026-09-15","2026-09-16")).Businesses);
        foreach(var denied in new[]{owner.Uid,unverified.Uid}){
            Assert.Equal(403,(await Assert.ThrowsAsync<DomainException>(()=>queries.Analytics(denied,"2026-09-15","2026-09-16"))).Status);
            await Assert.ThrowsAsync<DomainException>(()=>queries.Records(denied,a,"sales","2026-09-15","2026-09-16"));
            await Assert.ThrowsAsync<DomainException>(()=>queries.Report(denied,a,"summary","2026-09-15","2026-09-16"));
            await Assert.ThrowsAsync<DomainException>(()=>queries.Members(denied,a));
            await Assert.ThrowsAsync<DomainException>(()=>queries.Memberships(denied,owner.Uid));
            await Assert.ThrowsAsync<DomainException>(()=>queries.Directory(denied,"users","","2026-09-15","2026-09-16"));
        }
        await accounts.SavePhone(admin.Uid,new(AccountFixture.Phone()));
        await Assert.ThrowsAsync<DomainException>(()=>service.Bootstrap(admin.Uid,a));
        await Assert.ThrowsAsync<DomainException>(()=>new ReportQueries(connections).Query(admin.Uid,a,"summary","2026-09-15","2026-09-16"));
        await Assert.ThrowsAsync<DomainException>(()=>queries.Records(admin.Uid,a,"sales","2026-09-15","2026-09-16",0));
        await Assert.ThrowsAsync<DomainException>(()=>queries.Records(admin.Uid,a,"sales","2026-09-15","2026-09-16",1,101));
        await Assert.ThrowsAsync<DomainException>(()=>queries.Analytics(admin.Uid,"2026-09-16","2026-09-15"));
        Assert.Equal(404,(await Assert.ThrowsAsync<DomainException>(()=>queries.Records(admin.Uid,Guid.NewGuid(),"sales","2026-09-15","2026-09-16"))).Status);
        Assert.Equal(0,(await queries.Analytics(admin.Uid,"2020-01-01","2020-01-02",a)).Summary.RevenueCentavos);
    }
}
