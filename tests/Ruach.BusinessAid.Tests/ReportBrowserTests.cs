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
                "summary" => new FinancialSummary(1000, 400, 800, 600, -200, 2, 4),
                "sales-trend" => new[] { new SalesTrend("2026-09-15", 1000, 400, 600, 100, 500), new SalesTrend("2026-09-16", 0, 0, 0, 700, -700) },
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
        foreach (var width in new[] { 320, 390, 1366 })
        {
            await page.SetViewportSizeAsync(width, 900);
            await table.ScrollIntoViewIfNeededAsync();
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
            if (width < 640)
                Assert.True(await table.EvaluateAsync<bool>("table => table.parentElement.scrollWidth > table.parentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"reports-trends-{width}.png"), FullPage = true });
        }
    }
}
