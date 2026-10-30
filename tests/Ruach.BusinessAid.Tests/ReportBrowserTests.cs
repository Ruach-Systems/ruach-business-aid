using System.Text.Json;
using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task SalesTrendsShowExpensesAndNetProfitWithoutMobilePageOverflow()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync();
        var owner = new AppUser(Guid.NewGuid(), "Report tester", "reports@example.invalid", null) { PhoneNumber = "+639171234567" };
        var data = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Report shop", DefaultLocation = "Main" } };
        await MockApi(context, owner, data);
        await context.RouteAsync("**/api/reports/**", async route =>
        {
            object result = new Uri(route.Request.Url).AbsolutePath.Split('/').Last() switch
            {
                "summary" => new FinancialSummary(10000000000, 400, 800, 600, -200, 2, 4),
                "sales-trend" => new[] { new SalesTrend("2026-09-15", 1000, 400, 600, 100, 500), new SalesTrend("2026-09-16", 0, 0, 0, 700, -700) },
                "items" => new[] { new ItemReport(Guid.NewGuid(), "Large item with a long descriptive product name", 1000000, 10000000000, 10000000001, -1) },
                "expenses-by-category" => new[] { new ExpenseReport("Transportation and delivery services", 10000000000, 100m) },
                _ => Array.Empty<object>()
            };
            await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(result, Wire.Json),
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = route.Request.Headers.GetValueOrDefault("origin", "*"), ["Access-Control-Allow-Credentials"] = "true" } });
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Report shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/reports");
        var table = page.GetByRole(AriaRole.Table, new() { Name = "Sales trends", Exact = true });
        await Expect(table.Locator("thead th")).ToHaveTextAsync(new[] { "Period", "Revenue", "Item cost", "Gross profit", "Expenses", "Net profit" });
        await Expect(table.Locator("tbody tr")).ToHaveCountAsync(2);
        await Expect(table.Locator("tbody tr").First.Locator("td").Nth(4)).ToHaveTextAsync(Calculations.Money(100));
        await Expect(table.Locator("tbody tr").First.Locator("td").Nth(5)).ToHaveTextAsync(Calculations.Money(500));
        await Expect(table.Locator("tbody tr").Nth(1).Locator("td").Nth(5)).ToHaveTextAsync(Calculations.Money(-700));
        var applied = page.Locator(".applied-range");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "View reports", Exact = true })).ToHaveCountAsync(0);
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("today");
        await Expect(applied).ToContainTextAsync(Calculations.Today().ToString("MMM d, yyyy"));
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("custom");
        await page.Locator("#report-from").FillAsync("2026-09-15");
        await page.Locator("#report-to").FillAsync("2026-09-16");
        await Expect(applied).ToContainTextAsync("Sep 15, 2026");
        await Expect(applied).ToContainTextAsync("Sep 16, 2026");
        await Expect(page.Locator(".range-changed")).ToHaveCountAsync(0);
        await Expect(page.GetByText("Snapshotted cost", new() { Exact = true })).ToHaveCountAsync(0);
        foreach (var compact in new[] { false, true })
        foreach (var width in new[] { 320, 390, 470, 768, 1366 })
        {
            await page.EvaluateAsync("v => window.mashalUi.setCompact(v)", compact);
            await page.SetViewportSizeAsync(width, 900);
            await table.ScrollIntoViewIfNeededAsync();
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
            if (width < 640)
                Assert.True(await table.EvaluateAsync<bool>("e => getComputedStyle(e).display === 'table' && e.parentElement.scrollWidth > e.parentElement.clientWidth"));
            Assert.True(await page.Locator(".report-number").EvaluateAllAsync<bool>("elements => elements.every(e => getComputedStyle(e).whiteSpace === 'nowrap' && e.getBoundingClientRect().width <= e.closest('td').getBoundingClientRect().width + 1)"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"reports-trends-{width}-{compact}.png"), FullPage = true });
        }
    }
}
