using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Ruach.BusinessAid.Client.Components;
using Ruach.BusinessAid.Client.Pages;
using Ruach.BusinessAid.Client.Services;
using Ruach.BusinessAid.Shared;
using System.Text.Json;
using Xunit;

namespace Ruach.BusinessAid.Tests;

public partial class ComponentTests
{
    [Fact]
    public void NewQuantityIsEmptyWhileSalesBlankMeansZeroAndExistingCountIsPreserved()
    {
        using var context = Context();
        var received = context.Render<QuantityInput>(p => p.Add(x => x.Value, 1L).Add(x => x.Minimum, 1L).Add(x => x.StartEmpty, true));
        Assert.Equal("", received.Find("input").GetAttribute("value"));
        Assert.Equal("0", received.Find("input").GetAttribute("placeholder"));
        Assert.Equal("false", received.Find("input").GetAttribute("aria-invalid"));
        Assert.Empty(received.FindAll("[role=alert]"));
        long saved = 2;
        var sales = context.Render<QuantityInput>(p => p.Add(x => x.Value, 2L).Add(x => x.EmptyAsZero, true).Add(x => x.ValueChanged, v => saved = v));
        sales.Find("input").Input("");
        sales.Find("input").Blur();
        Assert.Equal(0, saved);
        Assert.Equal("", sales.Find("input").GetAttribute("value"));
        Assert.Equal("false", sales.Find("input").GetAttribute("aria-invalid"));
        var counted = context.Render<QuantityInput>(p => p.Add(x => x.Value, 24L));
        Assert.Equal("24", counted.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void NewMoneyIsEmptyButEditingZeroKeepsItsValue()
    {
        using var context = Context();
        var fresh = context.Render<MoneyInput>(p => p.Add(x => x.StartEmpty, true));
        Assert.Equal("", fresh.Find("input").GetAttribute("value"));
        Assert.Equal("0.00", fresh.Find("input").GetAttribute("placeholder"));
        fresh.Find("input").Input("0");
        fresh.Find("input").Blur();
        Assert.Equal("0.00", fresh.Find("input").GetAttribute("value"));
        var edit = context.Render<MoneyInput>(p => p.Add(x => x.Value, 0m));
        Assert.Equal("0.00", edit.Find("input").GetAttribute("value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12.345")]
    [InlineData("-1")]
    [InlineData("1e3")]
    [InlineData("1,25")]
    [InlineData("1.250,50")]
    [InlineData("100000000.01")]
    [InlineData("999999999999999999999999999999999999999999")]
    public void InvalidMoneyRemainsEditableAndDoesNotPublish(string text)
    {
        using var context = Context();
        decimal saved = 35;
        bool valid = true;
        var field = context.Render<MoneyInput>(p => p.Add(x => x.Value, saved).Add(x => x.ValueChanged, v => saved = v).Add(x => x.ValidityChanged, v => valid = v));
        field.Find("input").Input(text);
        field.Find("input").Blur();
        Assert.False(valid);
        Assert.Equal(35m, saved);
        Assert.Equal(text, field.Find("input").GetAttribute("value"));
        Assert.Equal("false", field.Find("input").GetAttribute("aria-invalid"));
        Assert.Empty(field.FindAll("[role=alert]"));
        field.Find("input").TriggerEvent("onvalidationsubmit", EventArgs.Empty);
        Assert.Equal("true", field.Find("input").GetAttribute("aria-invalid"));
        Assert.Equal(field.Find("[role=alert]").Id, field.Find("input").GetAttribute("aria-describedby"));
    }

    [Theory]
    [InlineData("₱1,250.50", "1250.50")]
    [InlineData(" 1250.5 ", "1250.50")]
    [InlineData("0", "0.00")]
    [InlineData("100,000,000.00", "100000000.00")]
    public void MoneyNormalizesOnlyValidAmounts(string text, string expected)
    {
        using var context = Context();
        decimal saved = 0;
        var field = context.Render<MoneyInput>(p => p.Add(x => x.ValueChanged, v => saved = v));
        field.Find("input").Input(text);
        field.Find("input").Blur();
        Assert.Equal(expected, field.Find("input").GetAttribute("value"));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), saved);
        Assert.Empty(field.FindAll("[role=alert]"));
        Assert.Equal("decimal", field.Find("input").GetAttribute("inputmode"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("-1")]
    [InlineData("1e3")]
    [InlineData("1000001")]
    [InlineData("9223372036854775807")]
    public void QuantityRejectsInvalidDraftWithoutPublishing(string text)
    {
        using var context = Context();
        long saved = 2;
        bool valid = true;
        var field = context.Render<QuantityInput>(p => p.Add(x => x.Value, saved).Add(x => x.ValueChanged, v => saved = v).Add(x => x.ValidityChanged, v => valid = v));
        field.Find("input").Input(text);
        field.Find("input").Blur();
        Assert.False(valid);
        Assert.Equal(2, saved);
        Assert.Equal(text, field.Find("input").GetAttribute("value"));
        Assert.Equal("numeric", field.Find("input").GetAttribute("inputmode"));
    }

    [Theory]
    [InlineData(0, "0", true)]
    [InlineData(1, "0", false)]
    [InlineData(1, "1", true)]
    [InlineData(0, "1000000", true)]
    public void QuantityAppliesFieldSpecificMinimum(long minimum, string text, bool expected)
    {
        using var context = Context();
        bool valid = false;
        var field = context.Render<QuantityInput>(p => p.Add(x => x.Minimum, minimum).Add(x => x.ValidityChanged, v => valid = v));
        field.Find("input").Input(text);
        Assert.Equal(expected, valid);
    }

    [Fact]
    public async Task InvalidPriceHidesProfitAndCannotSaveStaleAmount()
    {
        using var context = Context();
        var store = await LocalStore(context);
        var page = context.Render<InventoryEditor>();
        page.Find("#item-name").Change("Test item");
        page.Find("#item-minimum").Input("0");
        page.Find("#item-price").Input("35");
        page.Find("#item-cost").Input("10");
        Assert.Contains("₱25.00", page.Find(".cost-preview").TextContent);
        page.Find("#item-price").Input("");
        Assert.Equal(new[] { "—", "—" }, page.FindAll(".cost-preview strong").Select(x => x.TextContent));
        Assert.False(page.Find("button[type=submit], .form-actions button").HasAttribute("disabled"));
        page.Find("form").Submit(); // Also guard programmatic/Enter submission, not only the button.
        Assert.Empty(store.State!.Outbox);
        page.Find("#item-price").Input("40");
        Assert.Contains("₱30.00", page.Find(".cost-preview").TextContent);
        Assert.False(page.Find(".form-actions button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task SalesBoundsInvalidDraftAndSearchPreserveSafeTotals()
    {
        using var context = Context();
        var store = await LocalStore(context);
        store.Data.Items.Add(new() { Id = Guid.NewGuid(), Name = "Juice", IsActive = true, SellingPriceCentavos = 3500, UnitCostCentavos = 1000 });
        var page = context.Render<Sales>();
        Assert.Empty(page.FindAll(".sale-line"));
        page.Find(".product").Click();
        Assert.Equal("1", page.Find(".stepper input").GetAttribute("value"));
        page.Find(".stepper input").Input("1000000");
        Assert.True(page.FindAll(".stepper button")[1].HasAttribute("disabled"));
        page.Find(".stepper input").Input("9223372036854775807");
        Assert.All(page.FindAll(".totals-card strong"), x => Assert.Equal("—", x.TextContent));
        page.Find("#sale-search").Input("not a match");
        Assert.Empty(page.FindAll(".product"));
        Assert.Single(page.FindAll(".stepper input"));
        page.FindAll("form")[1].Submit();
        Assert.Empty(store.State!.Outbox);
        page.Find(".stepper input").Input("2");
        Assert.Equal("₱70.00", page.FindAll(".totals-card strong")[1].TextContent);
        Assert.False(page.Find(".sale-checkout button.primary").HasAttribute("disabled"));
    }

    [Fact]
    public async Task RegisterCanRemoveInvalidLinesAndRequiresReviewWhenAProductBecomesUnavailable()
    {
        using var context = Context();
        var store = await LocalStore(context);
        var juice = new Item { Id = Guid.NewGuid(), Name = "Juice", IsActive = true, SellingPriceCentavos = 3500 };
        var bread = new Item { Id = Guid.NewGuid(), Name = "Bread", IsActive = true, SellingPriceCentavos = 800 };
        store.Data.Items.AddRange([juice, bread]);
        var page = context.Render<Sales>();
        page.Find("[aria-label='Add Juice to sale']").Click();
        page.Find("[aria-label='Add Bread to sale']").Click();
        page.Find("[aria-label='Juice quantity sold']").Input("1000001");
        page.Find("[aria-label='Remove Juice from sale']").Click();
        page.Find("[aria-label='Add Juice to sale']").Click();
        Assert.Equal("1", page.Find("[aria-label='Juice quantity sold']").GetAttribute("value"));
        Assert.Equal("₱43.00", page.Find(".amount-due strong").TextContent);

        juice.IsActive = false;
        page.Find("#sale-search").Input("bread");
        page.Find("#record-sale").Submit();
        Assert.Empty(store.State!.Outbox);
        Assert.Equal("—", page.Find(".amount-due strong").TextContent);
        page.Find(".unavailable button").Click();
        Assert.Equal("₱8.00", page.Find(".amount-due strong").TextContent);
        page.Find("[aria-label='Decrease Bread quantity']").Click();
        Assert.Empty(page.FindAll(".sale-line"));
        Assert.True(page.Find(".sale-checkout button.primary").HasAttribute("disabled"));
    }

    [Fact]
    public async Task MoreThanFiveHundredLinesDoesNotCalculateOverflowingTotals()
    {
        using var context = Context();
        var store = await LocalStore(context);
        for (var i = 0; i < Rules.MaxSaleLines + 1; i++)
            store.Data.Items.Add(new() { Id = Guid.NewGuid(), Name = $"Item {i}", IsActive = true, SellingPriceCentavos = Rules.MaxCentavos, UnitCostCentavos = Rules.MaxCentavos });
        var page = context.Render<Sales>();
        foreach (var item in store.Items)
        {
            page.Find($"[aria-label='Add {item.Name} to sale']").Click();
            page.Find($"#sale-quantity-{item.Id}").Input("1000000");
        }
        Assert.Contains("at most 500", page.Markup);
        Assert.All(page.FindAll(".totals-card strong"), x => Assert.Equal("—", x.TextContent));
        Assert.False(page.Find(".sale-checkout button.primary").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ConflictReviewFormatsBothMoneyValuesAsPesos()
    {
        using var context = Context();
        var store = await LocalStore(context);
        var expense = new Expense { Id = Guid.NewGuid(), Description = "Fare", AmountCentavos = 125050 };
        store.State!.ServerData.Expenses.Add(expense);
        var local = JsonSerializer.SerializeToElement(new Expense { Id = expense.Id, Description = "Fare", AmountCentavos = 150000 }, Wire.Json);
        store.State.Outbox.Add(new() { Id = Guid.NewGuid(), EntityId = expense.Id, Type = "saveExpense", Error = "conflict", Payload = local, Projections = [new("expenses", local)] });
        var page = context.Render<SyncReview>();
        var row = page.FindAll("tr").Single(x => x.TextContent.Contains("Expense amount"));
        Assert.Contains("₱1,500.00", row.TextContent);
        Assert.Contains("₱1,250.50", row.TextContent);
        Assert.DoesNotContain("centavos", page.Markup);
    }

    private static async Task<BusinessState> LocalStore(BunitContext context)
    {
        var store = context.Services.GetRequiredService<BusinessState>();
        await store.ContinueLocally();
        await store.CreateLocalBusiness("Number entry test", "Main");
        return store;
    }
}
