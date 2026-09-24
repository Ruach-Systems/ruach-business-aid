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
        object payload = type == "recordSale" ? new SaleInput([], future) : new Expense { Description = "Fare", Category = "Transport", AmountCentavos = 100, ExpenseDate = future };
        var error = await Assert.ThrowsAsync<DomainException>(() => commands.Execute(Op(type, Guid.NewGuid(), payload)));
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

    [Fact]
    public void ItemProfitCalculationsHandleZeroCost()
    {
        Assert.Equal(300, Calculations.UnitProfit(500, 200));
        Assert.Equal(150m, Calculations.Markup(500, 200));
        Assert.Null(Calculations.Markup(500, 0));
        Assert.Equal(101, Calculations.Centavos(1.005m));
    }

    [Fact]
    public async Task LocalCommandsUseFixedCostSnapshotAndAllowNegativeStock()
    {
        var data = new AppData { Business = new() { Id = Guid.NewGuid() } };
        var commands = new CommandProcessor(new LocalRepository(data), false);
        var itemId = Guid.NewGuid();
        await commands.Execute(Op("saveItem", itemId, new Item { Name = " Juice ", SellingPriceCentavos = 500, UnitCostCentavos = 200, IsActive = true }));
        await Assert.ThrowsAsync<DomainException>(() => commands.Execute(Op("saveItem", Guid.NewGuid(), new Item { Name = "JUICE", SellingPriceCentavos = 500, UnitCostCentavos = 200, IsActive = true })));
        await commands.Execute(Op("receiveStock", Guid.NewGuid(), new ReceiptInput(itemId, 10, null)));
        Assert.Equal(200, data.Items.Single().UnitCostCentavos);
        Assert.Equal(200, data.Receipts.Single().UnitCostCentavos);
        await commands.Execute(Op("recordSale", Guid.NewGuid(), new SaleInput([new(itemId, 12)], "2026-09-16")));
        Assert.Equal(-2, data.Items.Single().CurrentQuantity);
        Assert.Equal(6000, data.Sales.Single().TotalRevenueCentavos);
        Assert.Equal(2400, data.Sales.Single().TotalCostCentavos);
        Assert.Equal(2, data.Movements.Count);
        Assert.Equal(2000, data.Receipts.Single().TotalCostCentavos);
        await commands.Execute(Op("saveItem", itemId, new Item { Name = "Juice", SellingPriceCentavos = 900, UnitCostCentavos = 300, IsActive = true }));
        var savedLine = data.Sales.Single().Lines.Single();
        Assert.Equal(500, savedLine.UnitPriceCentavos);
        Assert.Equal(200, savedLine.UnitCostCentavos);
    }

    [Fact]
    public async Task InactiveItemsCannotBeSoldButRemainEditable()
    {
        var data = new AppData { Business = new() { Id = Guid.NewGuid() } };
        var commands = new CommandProcessor(new LocalRepository(data), false);
        var itemId = Guid.NewGuid();
        await commands.Execute(Op("saveItem", itemId, new Item { Name = "Seasonal", SellingPriceCentavos = 300, UnitCostCentavos = 100, IsActive = false }));
        var error = await Assert.ThrowsAsync<DomainException>(() => commands.Execute(Op("recordSale", Guid.NewGuid(), new SaleInput([new(itemId, 1)], "2026-09-16"))));
        Assert.Contains("inactive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(data.Sales);
        await commands.Execute(Op("saveItem", itemId, new Item { Name = "Seasonal", SellingPriceCentavos = 350, UnitCostCentavos = 120, IsActive = true }));
        Assert.True(data.Items.Single().IsActive);
    }

    [Fact]
    public void LegacyOfflineStateHasOldModelVersionAndFrozenPayload()
    {
        var id = Guid.NewGuid();
        var raw = $$"""
        {"user":{"uid":"{{Guid.NewGuid()}}","displayName":"Existing owner","email":"test@example.invalid"},"cursor":12,"outbox":[{"id":"{{Guid.NewGuid()}}","entityId":"{{id}}","type":"saveExpense","expectedVersion":"AAAAAAAAB9E=","payload":{"id":"{{id}}","description":"Fare","amountCentavos":2500,"expenseDate":"2026-09-15"},"projections":[],"dependsOnPending":false,"prepared":true}]}
        """;
        var state = JsonSerializer.Deserialize<OfflineState>(raw, Wire.Json)!;
        Assert.Equal(0, state.ModelVersion);
        Assert.Equal(state.Outbox.Single().Payload.GetRawText(), LocalData.Clone(state).Outbox.Single().Payload.GetRawText());
    }

    private static Operation Op(string type, Guid id, object payload) => new(Guid.NewGuid(), id, type, null, JsonSerializer.SerializeToElement(payload, Wire.Json));
}
