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
    private static Operation SaveItem(Guid id, string name = "Milk", long price = 250, long cost = 100, string? version = null) => Op("saveItem", id, new Item { Name = name, SellingPriceCentavos = price, UnitCostCentavos = cost, MinimumQuantity = 1, IsActive = true }, version);

    [Fact]
    public async Task RetryIsIdempotentPullIsScopedAndOldProtocolIsRejected()
    {
        var (user, business, service) = await Setup();
        var item = SaveItem(Guid.NewGuid());
        await service.Push(user, new(DataModel.CurrentVersion, business, [item]));
        await service.Push(user, new(DataModel.CurrentVersion, business, [item]));
        Assert.Single((await service.Bootstrap(user, business)).Data.Items);
        var pull = await service.Pull(user, business, 0);
        Assert.Single(pull.Changes, x => x.CollectionName == "items");
        Assert.Equal(DataModel.CurrentVersion, pull.ModelVersion);
        Assert.Empty((await service.Pull(user, business, pull.Cursor)).Changes);
        var old = await Assert.ThrowsAsync<DomainException>(() => service.Push(user, new PushRequest(1, business, [SaveItem(Guid.NewGuid())])));
        Assert.Equal("client_upgrade_required", old.Code);
        var (other, _, _) = await Setup();
        await Assert.ThrowsAsync<DomainException>(() => service.Pull(other, business, 0));
    }

    [Fact]
    public async Task DuplicateNamesRollBackWholePush()
    {
        var (user, business, service) = await Setup();
        await Assert.ThrowsAsync<SqlException>(() => service.Push(user, new(DataModel.CurrentVersion, business, [SaveItem(Guid.NewGuid(), "Milk"), SaveItem(Guid.NewGuid(), " milk ")])));
        Assert.Empty((await service.Bootstrap(user, business)).Data.Items);
    }

    [Fact]
    public async Task StockSalesNegativeBalancesAndReportsRemainAtomic()
    {
        var (user, business, service) = await Setup();
        var itemId = Guid.NewGuid();
        await service.Push(user, new(DataModel.CurrentVersion, business,
        [
            SaveItem(itemId),
            Op("receiveStock", Guid.NewGuid(), new ReceiptInput(itemId, 10, "Opening stock"))
        ]));
        var sale = Op("recordSale", Guid.NewGuid(), new SaleInput([new(itemId, 12)], "2026-09-15"));
        await service.Push(user, new(DataModel.CurrentVersion, business, [sale]));
        await service.Push(user, new(DataModel.CurrentVersion, business, [sale]));
        var state = (await service.Bootstrap(user, business)).Data;
        Assert.Equal(-2, state.Items.Single().CurrentQuantity);
        Assert.Equal(2, state.Movements.Count);
        Assert.Single(state.Sales);
        Assert.Equal(3000, state.Sales.Single().TotalRevenueCentavos);
        Assert.Equal(1200, state.Sales.Single().TotalCostCentavos);
        Assert.Equal(1000, state.Receipts.Single().TotalCostCentavos);

        var expense = Guid.NewGuid();
        await service.Push(user, new(DataModel.CurrentVersion, business, [Op("saveExpense", expense, new Expense { Description = "Delivery", Category = "Transport", AmountCentavos = 50, ExpenseDate = "2026-09-15" })]));
        var reports = new ReportQueries(connections);
        var summary = (FinancialSummary)await reports.Query(user, business, "summary", "2026-09-01", "2026-09-30");
        Assert.Equal(1800, summary.GrossProfitCentavos);
        Assert.Equal(1750, summary.ProfitCentavos);
        var items = (List<ItemReport>)await reports.Query(user, business, "items", "2026-09-01", "2026-09-30");
        Assert.Equal(12, items.Single().Quantity);

        var savedExpense = (await service.Bootstrap(user, business)).Data.Expenses.Single();
        await service.Push(user, new(DataModel.CurrentVersion, business, [Op("deleteExpense", expense, new { }, Convert.ToBase64String(savedExpense.Version!))]));
        Assert.NotNull((await service.Bootstrap(user, business)).Data.Expenses.Single().DeletedAt);
    }

    [Fact]
    public async Task SaleSnapshotsSurviveItemCostChangesAndTenantReferencesFail()
    {
        var (user, business, service) = await Setup();
        var itemId = Guid.NewGuid();
        await service.Push(user, new(DataModel.CurrentVersion, business, [SaveItem(itemId, "Juice", 500, 200)]));
        await service.Push(user, new(DataModel.CurrentVersion, business, [Op("recordSale", Guid.NewGuid(), new SaleInput([new(itemId, 1)], "2026-09-15"))]));
        var item = (await service.Bootstrap(user, business)).Data.Items.Single();
        await service.Push(user, new(DataModel.CurrentVersion, business, [SaveItem(itemId, "Juice", 900, 300, Convert.ToBase64String(item.Version!))]));
        var line = (await service.Bootstrap(user, business)).Data.Sales.Single().Lines.Single();
        Assert.Equal(500, line.UnitPriceCentavos);
        Assert.Equal(200, line.UnitCostCentavos);

        var (other, otherBusiness, otherService) = await Setup();
        await Assert.ThrowsAsync<DomainException>(() => otherService.Push(other, new(DataModel.CurrentVersion, otherBusiness, [Op("receiveStock", Guid.NewGuid(), new ReceiptInput(itemId, 1, null))])));
    }
}
