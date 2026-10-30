using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task SalesHistoryPaginatesWithoutChangingFullRangeTotals()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 900 } });
        var owner = new AppUser(Guid.NewGuid(), "History tester", "history@example.invalid", null) { PhoneNumber = "+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "History shop", DefaultLocation = "Main" };
        var sales = Enumerable.Range(0, 24).Select(i => new Sale
        {
            Id = Guid.NewGuid(), BusinessId = business.Id, SaleDate = "2026-09-15",
            CreatedAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero).AddMinutes(-i),
            TotalItems = 2, TotalRevenueCentavos = 5000, TotalCostCentavos = 2000, TotalProfitCentavos = 3000,
            Lines = [new() { ItemId = Guid.NewGuid(), ItemName = $"History item {i}", Quantity = 2, UnitPriceCentavos = 2500, UnitCostCentavos = 1000, LineRevenueCentavos = 5000, LineCostCentavos = 2000 }]
        }).ToList();
        var deleted = new Sale { Id = Guid.NewGuid(), BusinessId = business.Id, SaleDate = "2026-09-22", DeletedAt = DateTimeOffset.UtcNow };
        var data = new AppData { Business = business, Sales = [..sales, deleted] };
        var other = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Empty shop", DefaultLocation = "Other" } };
        await MockApi(context, owner, data, [other]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open History shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();

        await page.GotoAsync(host.Url + "/sales/history");
        await page.Locator("#history-period").SelectOptionAsync("all");
        await Expect(page.Locator(".history-sale")).ToHaveCountAsync(10);
        await Expect(page.Locator(".history-count")).ToContainTextAsync("24 sales · ₱1,200.00");
        await Expect(page.GetByRole(AriaRole.Button,new() { Name="Previous", Exact=true })).ToBeDisabledAsync();
        await page.GetByRole(AriaRole.Button,new() { Name="Next", Exact=true }).ClickAsync();
        await Expect(page.Locator(".history-pagination")).ToContainTextAsync("11–20 of 24");
        await Expect(page.Locator(".history-sale")).ToHaveCountAsync(10);
        await Expect(page.Locator(".history-count")).ToContainTextAsync("24 sales · ₱1,200.00");
        await page.GetByRole(AriaRole.Button,new() { Name="Next", Exact=true }).ClickAsync();
        await Expect(page.Locator(".history-sale")).ToHaveCountAsync(4);
        await Expect(page.GetByRole(AriaRole.Button,new() { Name="Next", Exact=true })).ToBeDisabledAsync();
        await page.Locator(".history-sale summary").First.ClickAsync();
        await Expect(page.Locator(".history-sale").First.Locator(".history-items")).ToContainTextAsync("2 × ₱25.00 each");
        await page.Locator("#history-period").SelectOptionAsync("custom");
        await page.Locator("#history-from").FillAsync("2026-09-15");
        await page.Locator("#history-to").FillAsync("2026-09-15");
        await Expect(page.Locator(".history-pagination")).ToContainTextAsync("1–10 of 24");
        foreach(var width in new[] {320,390,470}) {
            await page.Locator(".history-sale[open] summary").EvaluateAllAsync<object>("els => els.forEach(e => e.click())");
            await page.SetViewportSizeAsync(width,900);
            foreach(var compact in new[] {false,true}) {
                await page.EvaluateAsync("v => window.mashalUi.setCompact(v)",compact);
                var bounds=await page.Locator(".history-sale").First.BoundingBoxAsync();
                Assert.InRange(bounds!.Height,44,90);
            }
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        }
    }

    [BrowserFact]
    public async Task SalesHistoryLinksFilterDatesShowItemsAndStayAvailableOffline()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 900 } });
        var owner = new AppUser(Guid.NewGuid(), "History tester", "history@example.invalid", null) { PhoneNumber = "+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "History shop", DefaultLocation = "Main" };
        var sales = Enumerable.Range(0, 7).Select(i => new Sale
        {
            Id = Guid.NewGuid(), BusinessId = business.Id, SaleDate = $"2026-09-{15 + i:D2}",
            CreatedAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero).AddMinutes(-i),
            TotalItems = 2, TotalRevenueCentavos = 5000, TotalCostCentavos = 2000, TotalProfitCentavos = 3000,
            Lines = [new() { ItemId = Guid.NewGuid(), ItemName = $"History item {i}", Quantity = 2, UnitPriceCentavos = 2500, UnitCostCentavos = 1000, LineRevenueCentavos = 5000, LineCostCentavos = 2000 }]
        }).ToList();
        var deleted = new Sale { Id = Guid.NewGuid(), BusinessId = business.Id, SaleDate = "2026-09-22", DeletedAt = DateTimeOffset.UtcNow };
        var data = new AppData { Business = business, Sales = [..sales, deleted] };
        var other = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Empty shop", DefaultLocation = "Other" } };
        await MockApi(context, owner, data, [other]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open History shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        var recent = page.Locator(".split > section").Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Recent sales", Exact = true }) });
        await recent.GetByRole(AriaRole.Link, new() { Name = "View all", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(host.Url + "/sales/history");
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(7);
        await page.GotoAsync(host.Url + "/sales");
        var preview = page.Locator("#sales-history");
        await Expect(preview.Locator("a.sale-history-link")).ToHaveCountAsync(5);
        await Expect(preview.Locator("a.sale-history-link").First).ToHaveAttributeAsync("href", $"/sales/{sales[6].Id}");
        await Expect(preview.GetByRole(AriaRole.Link, new() { Name = "View all", Exact = true })).ToHaveAttributeAsync("href", "/sales/history");
        await preview.GetByRole(AriaRole.Link, new() { Name = "View all sales history by date", Exact = true }).ClickAsync();
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(7);
        foreach (var preset in new[] { "today", "yesterday", "week", "month" })
        {
            await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync(preset);
            var today = Calculations.Today();
            var expectedTo = preset == "yesterday" ? today.AddDays(-1) : today;
            var expectedFrom = preset switch {
                "yesterday" => expectedTo,
                "week" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
                "month" => new DateOnly(today.Year, today.Month, 1),
                _ => today
            };
            await Expect(page.GetByLabel("From", new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(page.GetByLabel("To", new() { Exact = true })).ToHaveCountAsync(0);
        }
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("custom");
        await page.GetByLabel("From", new() { Exact = true }).FillAsync("2026-09-16");
        await page.GetByLabel("From", new() { Exact = true }).PressAsync("Tab");
        await page.GetByLabel("To", new() { Exact = true }).FillAsync("2026-09-17");
        await page.GetByLabel("To", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(2);
        await Expect(page.Locator("details.history-sale").First).ToContainTextAsync("History item 2");
        await page.Locator("details.history-sale summary").First.ClickAsync();
        var items = page.Locator("details.history-sale").First.Locator(".history-items");
        await Expect(items).ToContainTextAsync("2 × ₱25.00 each");
        await Expect(items).ToContainTextAsync("₱50.00");
        foreach (var width in new[] { 320, 390, 1366 })
        {
            await page.SetViewportSizeAsync(width, 900);
            await items.ScrollIntoViewIfNeededAsync();
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"sales-history-{width}.png"), FullPage = true });
        }
        await items.GetByRole(AriaRole.Link, new() { Name = "View sale details", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sale details", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Back to sales history", Exact = true }).ClickAsync();
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("custom");
        await page.GetByLabel("From", new() { Exact = true }).FillAsync("2026-09-23");
        await page.GetByLabel("From", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No sales in this date range" })).ToBeVisibleAsync();
        await page.GetByLabel("To", new() { Exact = true }).FillAsync("2026-09-15");
        await page.GetByLabel("To", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Enter a valid date within the allowed range.");
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(7);
        await page.WaitForFunctionAsync("() => !!navigator.serviceWorker.controller");
        await context.SetOfflineAsync(true);
        await page.ReloadAsync();
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("all");
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(7);
        await Expect(page.GetByText("Showing sales saved on this device.", new() { Exact = false })).ToBeVisibleAsync();
        await context.SetOfflineAsync(false);
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Empty shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/sales/history");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No sales recorded yet", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("details.history-sale")).ToHaveCountAsync(0);
    }
}

