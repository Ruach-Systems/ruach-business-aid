using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task MobileNumericEntryRejectsInvalidDraftsAndSavesExactCentavos()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var account = new AppUser(Guid.NewGuid(), "Numeric tester", "numeric@example.invalid", null) { PhoneNumber = "+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = account.Uid, Name = "Numeric test shop", DefaultLocation = "Main" };
        var item = new Item { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Juice", IsActive = true, SellingPriceCentavos = 3500, UnitCostCentavos = 1000, Version = [0,0,0,0,0,0,0,1] };
        await MockApi(context, account, new() { Business = business, Items = [item] });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Numeric test shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview", Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/items/new");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "New item", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("#item-price")).ToHaveValueAsync("");
        await Expect(page.Locator("#item-price")).ToHaveAttributeAsync("placeholder", "0.00");
        await Expect(page.Locator("#item-cost")).ToHaveValueAsync("");
        await page.Locator("#item-name").FillAsync("Focus test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save item", Exact = true }).ClickAsync();
        await Expect(page.Locator("#item-price")).ToBeFocusedAsync();
        var price = page.GetByLabel("Selling price (₱)", new() { Exact = true });
        await price.FillAsync("35");
        await page.GetByLabel("Unit cost (₱)", new() { Exact = true }).FillAsync("10");
        await Expect(page.Locator(".cost-preview")).ToContainTextAsync("₱25.00");
        await price.FillAsync("12.345");
        await price.PressAsync("Tab");
        await Expect(price).ToHaveValueAsync("12.345");
        await Expect(price).ToHaveAttributeAsync("aria-invalid", "true");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save item", Exact = true }).ClickAsync();
        await Expect(price).ToBeFocusedAsync();
        await Expect(page.Locator(".cost-preview strong").First).ToHaveTextAsync("—");
        foreach (var compact in new[] { false, true })
        foreach (var width in new[] { 390, 470, 768 })
        {
            await page.EvaluateAsync("compact => mashalUi.setCompact(compact)", compact);
            await page.SetViewportSizeAsync(width, 844);
            var priceBox = await price.BoundingBoxAsync();
            var costBox = await page.Locator("#item-cost").BoundingBoxAsync();
            Assert.InRange(Math.Abs(priceBox!.Y - costBox!.Y), 0, 1);
            Assert.InRange(Math.Abs(priceBox.Height - costBox.Height), 0, 1);
        }
        await page.EvaluateAsync("() => mashalUi.setCompact(false)");
        await price.FillAsync("999999999999999999999999999999999999");
        await Expect(price).ToHaveAttributeAsync("aria-invalid", "true");
        await price.FillAsync("40");
        await Expect(page.Locator(".cost-preview")).ToContainTextAsync("₱30.00");

        await page.GotoAsync(host.Url + "/sales");
        var quantity = page.GetByLabel("Juice quantity sold", new() { Exact = true });
        var increase = page.GetByRole(AriaRole.Button, new() { Name = "Increase Juice quantity", Exact = true });
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Decrease Juice quantity", Exact = true })).ToBeDisabledAsync();
        await quantity.FillAsync("1000000");
        await Expect(increase).ToBeDisabledAsync();
        await quantity.FillAsync("9223372036854775807");
        foreach (var compact in new[] { false, true })
        foreach (var width in new[] { 320, 390, 470, 768 })
        {
            await page.EvaluateAsync("compact => mashalUi.setCompact(compact)", compact);
            await page.SetViewportSizeAsync(width, 844);
            var inputBox = await quantity.BoundingBoxAsync();
            var minusBox = await page.GetByRole(AriaRole.Button, new() { Name = "Decrease Juice quantity", Exact = true }).BoundingBoxAsync();
            var plusBox = await increase.BoundingBoxAsync();
            Assert.InRange(Math.Abs(inputBox!.Y - minusBox!.Y), 0, 1);
            Assert.InRange(Math.Abs(inputBox.Y - plusBox!.Y), 0, 1);
            Assert.InRange(Math.Abs(inputBox.Height - plusBox.Height), 0, 1);
            var errorBox = await page.Locator(".stepper .numeric-error").BoundingBoxAsync();
            Assert.True(errorBox!.Y >= inputBox.Y + inputBox.Height);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        }
        await page.EvaluateAsync("() => mashalUi.setCompact(false)");
        await page.SetViewportSizeAsync(390, 844);
        await page.Locator(".sale-checkout button").ClickAsync();
        await Expect(quantity).ToBeFocusedAsync();
        await Expect(page.Locator(".totals-card strong").Nth(1)).ToHaveTextAsync("—");
        await page.GetByLabel("Search items to sell", new() { Exact = true }).FillAsync("unmatched");
        await Expect(quantity).ToBeVisibleAsync();
        await quantity.FillAsync("2");
        foreach (var width in new[] { 390, 470, 768 })
        {
            await page.SetViewportSizeAsync(width, 844);
            var card = await page.Locator(".sale-line").First.BoundingBoxAsync();
            var warning = await page.Locator(".sale-line > .stock-warning").First.BoundingBoxAsync();
            var controls = await page.Locator(".stepper").First.BoundingBoxAsync();
            Assert.True(warning!.Width > card!.Width - 50);
            Assert.True(warning.Y >= controls!.Y + controls.Height);
        }
        await page.SetViewportSizeAsync(390, 844);
        await Expect(page.Locator(".totals-card strong").Nth(1)).ToHaveTextAsync("₱70.00");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save 2 items", Exact = true }).ClickAsync();
        await Expect(quantity).ToHaveValueAsync("");
        var state = await ReadState(page, account.Uid);
        Assert.Equal(7000, state.GetProperty("data").GetProperty("sales")[0].GetProperty("totalRevenueCentavos").GetInt64());

        await page.GotoAsync(host.Url + "/expenses");
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Audit fare");
        var amount = page.GetByLabel("Amount (₱)", new() { Exact = true });
        await amount.FillAsync("₱1,250.50");
        await amount.PressAsync("Tab");
        await Expect(amount).ToHaveValueAsync("1250.50");
        await amount.FillAsync("");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
        await Expect(amount).ToBeFocusedAsync();
        await amount.FillAsync("₱1,250.50");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Edit Audit fare expense", Exact = true })).ToBeVisibleAsync();
        await Expect(amount).ToHaveValueAsync("");
        state = await ReadState(page, account.Uid);
        Assert.Equal(125050, state.GetProperty("data").GetProperty("expenses")[0].GetProperty("amountCentavos").GetInt64());
        await page.GetByRole(AriaRole.Button, new() { Name = "Edit Audit fare expense", Exact = true }).ClickAsync();
        await Expect(amount).ToHaveValueAsync("1250.50");
        await amount.FillAsync("1e3");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel edit", Exact = true }).ClickAsync();
        await Expect(amount).ToHaveValueAsync("");
        await Expect(amount).ToHaveAttributeAsync("aria-invalid", "true");

        await page.GotoAsync(host.Url + "/stock/add?item=" + item.Id);
        var received = page.GetByLabel("Quantity received", new() { Exact = true });
        await Expect(received).ToHaveAttributeAsync("inputmode", "numeric");
        await received.FillAsync("0");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save stock", Exact = true }).ClickAsync();
        await Expect(received).ToBeFocusedAsync();
        await Expect(page.Locator(".stock-cost-total strong")).ToHaveTextAsync("—");
        await received.FillAsync("3");
        await Expect(page.Locator(".stock-cost-total strong")).ToHaveTextAsync("₱30.00");
        await received.FillAsync("1.5");
        foreach (var width in new[] { 320, 390 })
        {
            await page.SetViewportSizeAsync(width, 844);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        }
        Directory.CreateDirectory(Path.Combine(host.Root, "artifacts", "browser"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", "numeric-entry-error-mobile.png"), FullPage = true });
        // Every single-column numeric form keeps errors below the control, including compact mode.
        foreach (var compact in new[] { false, true })
        foreach (var route in new[] { "/expenses", "/stock/add?item=" + item.Id, "/stock/adjust?item=" + item.Id, "/items/new" })
        {
            await page.GotoAsync(host.Url + route);
            await Expect(page.Locator("main h1")).ToBeVisibleAsync();
            await page.EvaluateAsync("compact => mashalUi.setCompact(compact)", compact);
            var selector = route.StartsWith("/expenses") ? "#expense-amount" : route.StartsWith("/stock/add") ? "#receipt-quantity" : route.StartsWith("/stock/adjust") ? "#adjust-count" : "#item-minimum";
            var control = page.Locator(selector);
            var before = await control.BoundingBoxAsync();
            await control.FillAsync("-1");
            var after = await control.BoundingBoxAsync();
            Assert.InRange(Math.Abs(before!.Height - after!.Height), 0, 1);
            var describedBy = await control.GetAttributeAsync("aria-describedby");
            var errorBox = await page.Locator("#" + describedBy).BoundingBoxAsync();
            Assert.True(errorBox!.Y >= after.Y + after.Height);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
        }
        Assert.Empty(errors);
    }
}
