using System.Text.Json;
using Dapper;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Api.Data;
using Microsoft.Data.SqlClient;
using Xunit;
namespace Mashal.Tests;

[Trait("Category", "Integration")]
public class SqlTests
{
    private readonly SqlConnectionFactory connections = new(Environment.GetEnvironmentVariable("ConnectionStrings__Mashal") ?? throw new InvalidOperationException("Integration tests require a migrated, dedicated test database."));
    private async Task<(Guid User, Guid Business, BusinessService Service)> Setup()
    {
        var identities = new IdentityRepository(connections);
        var account = await identities.SignIn(Guid.NewGuid().ToString(), "Integration tester", "test@example.invalid", null);
        var service = new BusinessService(connections, identities);
        var business = await AccountFixture.Provision(connections, account.Uid);
        return (account.Uid, business, service);
    }
    private static Operation Op(string type, Guid id, object input, string? version = null) => new(Guid.NewGuid(), id, type, version, JsonSerializer.SerializeToElement(input, Wire.Json));
    private static Operation Item(Guid id, string name = "Milk") => Op("saveInventoryItem", id, new { Name = name, BaseUnit = "pc", UnitKind = "count", MinimumQuantity = 1, IsActive = true });
    [Fact]
    public async Task RetryIsIdempotentAndPullIsScoped()
    {
        var (user, business, service) = await Setup(); var id = Guid.NewGuid(); var item = Item(id);
        await service.Push(user, new(business, [item])); await service.Push(user, new(business, [item]));
        var state = await service.Bootstrap(user, business);
        Assert.Single(state.Data.InventoryItems);
        var pull = await service.Pull(user, business, 0); Assert.Equal(2, pull.Changes.Count);
        Assert.Empty((await service.Pull(user, business, pull.Cursor)).Changes);
        var (other, _, _) = await Setup();
        var forbidden = await Assert.ThrowsAsync<DomainException>(() => service.Pull(other, business, 0)); Assert.Equal(403, forbidden.Status);
        await Assert.ThrowsAsync<DomainException>(() => service.Push(other, new(business, [item])));
        await Assert.ThrowsAsync<DomainException>(() => service.Push(user, new(business, [item with { Payload = JsonSerializer.SerializeToElement(new { Name = "Changed" }, Wire.Json) }])));
    }
    [Fact]
    public async Task DuplicateNamesRollBackWholePush()
    {
        var (user, business, service) = await Setup();
        await Assert.ThrowsAsync<SqlException>(() => service.Push(user, new(business, [Item(Guid.NewGuid(), "Milk"), Item(Guid.NewGuid(), " milk ")])));
        Assert.Empty((await service.Bootstrap(user, business)).Data.InventoryItems);
    }
    [Fact]
    public async Task ReceiptsAndSalesMaintainAtomicBalancesAndReports()
    {
        var (user, business, service) = await Setup(); var itemId = Guid.NewGuid(); var productId = Guid.NewGuid();
        await service.Push(user, new(business, [Item(itemId),
   Op("receiveStock",Guid.NewGuid(),new{InventoryItemId=itemId,Quantity=10,Unit="pc",ToBase=1,TotalCostCentavos=1000}),
   Op("saveProduct",productId,new{Name="Prepared Milk",InventoryMode="prepared",FinishedInventoryItemId=itemId,SellingPriceCentavos=250,ManualCostCentavos=0,IsActive=true,Recipe=Array.Empty<object>()})]));
        var sale = Op("recordSale", Guid.NewGuid(), new { Quantities = new Dictionary<Guid, decimal> { { productId, 2 } }, DeductInventory = true, SaleDate = "2026-09-15" });
        await service.Push(user, new(business, [sale])); await service.Push(user, new(business, [sale]));
        var state = (await service.Bootstrap(user, business)).Data;
        Assert.Equal(8, state.InventoryItems.Single().CurrentQuantity); Assert.Equal(100, state.InventoryItems.Single().AverageCostCentavos);
        Assert.Equal(2, state.Movements.Count); Assert.Single(state.Sales);
        Assert.Equal("2026-09-15", state.Sales.Single().SaleDate);
        Assert.Equal(500, state.Sales.Single().TotalRevenueCentavos); Assert.Equal(200, state.Sales.Single().TotalCostCentavos);
        var expense = Guid.NewGuid();
        await service.Push(user, new(business, [Op("saveExpense", expense, new { Description = "Delivery", Category = "Transport", AmountCentavos = 50, ExpenseDate = "2026-09-15" })]));
        var reports = new ReportQueries(connections);
        var json = JsonSerializer.SerializeToElement(await reports.Query(user, business, "summary", "2026-09-01", "2026-09-30"), new JsonSerializerOptions { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(250, json.GetProperty("profitCentavos").GetInt64());
        var refreshed = await service.Bootstrap(user, business); var saved = refreshed.Data.Expenses.Single();
        await service.Push(user, new(business, [Op("deleteExpense", expense, new { }, Convert.ToBase64String(saved.Version!))]));
        Assert.NotNull((await service.Bootstrap(user, business)).Data.Expenses.Single().DeletedAt);
        Assert.Single(state.Sales.Single().Lines);
    }
    [Fact]
    public async Task ConcurrentEditsConflictAndTenantReferencesFail()
    {
        var (user, business, service) = await Setup(); var id = Guid.NewGuid();
        await service.Push(user, new(business, [Item(id)]));
        var item = (await service.Bootstrap(user, business)).Data.InventoryItems.Single();
        var edit = Item(id, "Milk updated") with { ExpectedVersion = Convert.ToBase64String(item.Version!) };
        await service.Push(user, new(business, [edit]));
        await Assert.ThrowsAsync<DomainException>(() => service.Push(user, new(business, [edit with { Id = Guid.NewGuid() }])));
        var (other, otherBusiness, otherService) = await Setup();
        await Assert.ThrowsAsync<DomainException>(() => otherService.Push(other, new(otherBusiness, [Op("receiveStock", Guid.NewGuid(), new { InventoryItemId = id, Quantity = 1, Unit = "pc", ToBase = 1, TotalCostCentavos = 100 })])));
    }
    [Fact]
    public async Task ProductionSnapshotsSurviveLaterCostChanges()
    {
        var (user, business, service) = await Setup(); var ingredient = Guid.NewGuid(); var output = Guid.NewGuid(); var product = Guid.NewGuid(); var batch = Guid.NewGuid();
        await service.Push(user, new(business, [Item(ingredient,"Ingredient"),Item(output,"Output"),
   Op("receiveStock",Guid.NewGuid(),new{InventoryItemId=ingredient,Quantity=20,Unit="pc",ToBase=1,TotalCostCentavos=2000}),
   Op("saveProduct",product,new{Name="Product",InventoryMode="prepared",FinishedInventoryItemId=output,SellingPriceCentavos=500,IsActive=true,Recipe=new[]{new{InventoryItemId=ingredient,Quantity=2}}}),
   Op("saveBatch",batch,new{ProductId=product,PlannedYield=3})]));
        var draft = (await service.Bootstrap(user, business)).Data.Batches.Single();
        await service.Push(user, new(business, [Op("completeBatch", batch, new { ActualYield = 3, ActualQuantities = new Dictionary<Guid, decimal> { { ingredient, 6 } } }, Convert.ToBase64String(draft.Version!))]));
        var state = (await service.Bootstrap(user, business)).Data;
        Assert.Equal(600, state.Batches.Single().TotalCostCentavos);
        Assert.Equal(14, state.InventoryItems.Single(x => x.Id == ingredient).CurrentQuantity);
        Assert.Equal(3, state.InventoryItems.Single(x => x.Id == output).CurrentQuantity);
        await service.Push(user, new(business, [Op("receiveStock", Guid.NewGuid(), new { InventoryItemId = ingredient, Quantity = 10, Unit = "pc", ToBase = 1, TotalCostCentavos = 5000 })]));
        Assert.Equal(600, (await service.Bootstrap(user, business)).Data.Batches.Single().TotalCostCentavos);
    }
}
