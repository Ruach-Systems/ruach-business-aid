using System.Text.Json;
using Mashal.BusinessAid.Shared;
using Mashal.BusinessAid.Client.Services;
using Xunit;
namespace Mashal.BusinessAid.Tests;

public class ClientLogicTests
{
    [Theory]
    [InlineData("recordSale")]
    [InlineData("saveExpense")]
    public async Task FutureBusinessDatesAreRejected(string type)
    {
        var data = new AppData();
        var commands = new CommandProcessor(new LocalRepository(data), false);
        var future = Calculations.Today().AddDays(1).ToString("yyyy-MM-dd");
        object payload = type == "recordSale"
            ? new SaleInput(new(), false, future, null)
            : new Expense { Description = "Fare", Category = "Transport", AmountCentavos = 100, ExpenseDate = future };
        var operation = new Operation(Guid.NewGuid(), Guid.NewGuid(), type, null, JsonSerializer.SerializeToElement(payload, Wire.Json));
        var error = await Assert.ThrowsAsync<DomainException>(() => commands.Execute(operation));
        Assert.Equal("validation", error.Code);
        Assert.Contains("future date", error.Message);
        Assert.Empty(data.Sales);
        Assert.Empty(data.Expenses);
    }

    [Fact]
    public void BusinessDatesUseManilaAndMondayWeeks()
    {
        var now = new DateTimeOffset(2026, 9, 13, 16, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 9, 14), Calculations.Today(now));
        Assert.Equal(("2026-09-14", "2026-09-14"), Calculations.Period("week", now));
        Assert.Equal(("2026-09-01", "2026-09-14"), Calculations.Period("month", now));
    }
    [Theory]
    [InlineData(false, true, false, "/dashboard", "/dashboard")]
    [InlineData(true, false, false, "/dashboard", "/sign-in")]
    [InlineData(true, true, false, "/dashboard", "/onboarding")]
    [InlineData(true, true, true, "/onboarding", "/dashboard")]
    [InlineData(true, true, true, "/reports", "/reports")]
    public void RoutesWaitForHydration(bool ready, bool signedIn, bool business, string path, string expected) => Assert.Equal(expected, Calculations.Route(ready, signedIn, business, path));
    [Fact]
    public void CostsAndConversionsMatchOperations()
    {
        Assert.Equal(1000, Calculations.Conversion("kg", "g"));
        Assert.Throws<DomainException>(() => Calculations.Conversion("L", "g"));
        var item = new InventoryItem { Id = Guid.NewGuid(), AverageCostCentavos = 125 };
        var product = new Product { InventoryMode = "prepared", Recipe = [new() { InventoryItemId = item.Id, Quantity = 1.5m }] };
        Assert.Equal(188, Calculations.ProductCost(product, [item]));
        Assert.Equal(101, Calculations.Centavos(1.005m));
        Assert.Equal(200, Rules.Average(-2, 100, 10, 2000));
        Assert.Equal(30, Calculations.PerItemQuantity(300, 10));
        Assert.Equal(300, Calculations.BatchQuantity(30, 10));
    }
    [Fact]
    public async Task LocalCommandsValidateNamesAndProduceStockAndFinancialSnapshots()
    {
        var business = Guid.NewGuid(); var data = new AppData { Business = new() { Id = business } };
        var repo = new LocalRepository(data); var commands = new CommandProcessor(repo, false);
        var item = Guid.NewGuid();
        Operation Op(string type, Guid id, object payload) => new(Guid.NewGuid(), id, type, null, JsonSerializer.SerializeToElement(payload, Wire.Json));
        await commands.Execute(Op("saveInventoryItem", item, new InventoryItem { Id = item, Name = " Flour ", BaseUnit = "g", UnitKind = "mass", IsActive = true }));
        await Assert.ThrowsAsync<DomainException>(() => commands.Execute(Op("saveInventoryItem", Guid.NewGuid(), new InventoryItem { Name = "FLOUR", BaseUnit = "g", UnitKind = "mass", IsActive = true })));
        await commands.Execute(Op("receiveStock", Guid.NewGuid(), new ReceiptInput(item, 1, "kg", 1000, 10000, null)));
        Assert.Equal(1000, data.InventoryItems.Single().CurrentQuantity);
        Assert.Equal(10, data.InventoryItems.Single().AverageCostCentavos);
        var product = Guid.NewGuid();
        await commands.Execute(Op("saveProduct", product, new Product { Id = product, Name = "Pastry", InventoryMode = "untracked", SellingPriceCentavos = 500, ManualCostCentavos = 200, IsActive = true }));
        await commands.Execute(Op("recordSale", Guid.NewGuid(), new SaleInput(new() { { product, 3 } }, true, "2026-09-16", new() { { product, 500 } })));
        Assert.Equal(1500, data.Sales.Single().TotalRevenueCentavos);
        Assert.Equal(600, data.Sales.Single().TotalCostCentavos);
        Assert.Single(data.Movements);
        await commands.Execute(Op("saveProduct", product, new Product { Id = product, Name = "Pastry", InventoryMode = "untracked", SellingPriceCentavos = 900, IsActive = true }));
        Assert.Equal(500, data.Sales.Single().Lines.Single().UnitPriceCentavos);
    }
    [Fact]
    public void ExistingJavascriptStateRetainsFrozenOperationPayload()
    {
        var id = Guid.NewGuid(); var business = Guid.NewGuid();
        var raw = $$"""
        {"user":{"uid":"{{Guid.NewGuid()}}","displayName":"Existing owner","email":"test@example.invalid"},"data":{"business":{"id":"{{business}}","ownerUid":"{{Guid.NewGuid()}}","name":"Shop","defaultLocation":"Main","createdAt":"2026-09-15T00:00:00Z","updatedAt":"2026-09-15T00:00:00Z"},"inventoryItems":[],"products":[],"sales":[],"expenses":[],"receipts":[],"movements":[],"batches":[]},"serverData":{"business":null,"inventoryItems":[],"products":[],"sales":[],"expenses":[],"receipts":[],"movements":[],"batches":[]},"cursor":12,"outbox":[{"id":"{{Guid.NewGuid()}}","entityId":"{{id}}","type":"saveExpense","expectedVersion":"AAAAAAAAB9E=","payload":{"id":"{{id}}","description":"Fare","amountCentavos":2500,"expenseDate":"2026-09-15"},"projections":[],"dependsOnPending":false,"prepared":true}]}
        """;
        var state = JsonSerializer.Deserialize<OfflineState>(raw, Wire.Json)!;
        var frozen = state.Outbox.Single();
        var payload = frozen.Payload.GetRawText();
        var copy = LocalData.Clone(state);
        Assert.True(copy.Outbox.Single().Prepared);
        Assert.Equal(payload, copy.Outbox.Single().Payload.GetRawText());
        Assert.Equal(12, copy.Cursor);
        Assert.Equal("AAAAAAAAB9E=", copy.Outbox.Single().ExpectedVersion);
    }
}
