using System.Text.Json;
using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task OwnerBusinessOverviewUsesScopedRoutesAndMobileIcon()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless=true, Channel=Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new() { HasTouch=true, ViewportSize=new() { Width=390, Height=844 } });
        var user = new AppUser(Guid.NewGuid(),"Owner","owner@example.invalid",null) { PhoneNumber="+639181234567" };
        var business = new Business { Id=Guid.NewGuid(),OwnerUid=user.Uid,Name="Owner shop",DefaultLocation="Cebu",CreatedAt=DateTimeOffset.UtcNow };
        await MockApi(context,user,new() { Business=business });
        var paths = new List<string>();
        var reportRequests = new List<Uri>();
        await context.RouteAsync("**/api/businesses/**",async route => {
            var request = new Uri(route.Request.Url);
            var path = request.AbsolutePath; paths.Add(path);
            reportRequests.Add(request);
            var end = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(route.Request.Url).Query)["to"].ToString();
            var start = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(route.Request.Url).Query)["from"].ToString();
            var currentDay = Calculations.Today();
            var trend = new SalesTrend[]
            {
                new(currentDay.AddDays(-1).ToString("yyyy-MM-dd"), 6500, 3000, 3500, 4000, -500),
                new(currentDay.ToString("yyyy-MM-dd"), 3500, 3000, 500, 1000, -500)
            }.Where(day => string.CompareOrdinal(day.Period, start) >= 0 && string.CompareOrdinal(day.Period, end) <= 0).ToList();
            var summary = new FinancialSummary(trend.Sum(day => day.RevenueCentavos), trend.Sum(day => day.CostCentavos),
                trend.Sum(day => day.ExpenseCentavos), trend.Sum(day => day.GrossProfitCentavos),
                trend.Sum(day => day.ProfitCentavos), trend.Count, trend.Count * 2);
            object body = path.EndsWith("/analytics") ? new AdminAnalytics(summary, trend, [])
                : path.EndsWith("/members") ? new List<AdminMember> { new(user.Uid,user.DisplayName,user.Email,"Owner") }
                : path.Contains("/records/") ? new AdminRecords(new(),0,1,25)
                : path.EndsWith("/items") ? new List<ItemReport>() : (object)new List<ExpenseReport>();
            await route.FulfillAsync(new() { Status=200,ContentType="application/json",Body=JsonSerializer.Serialize(body,Wire.Json),Headers=new Dictionary<string,string>{{"Access-Control-Allow-Origin",route.Request.Headers.GetValueOrDefault("origin","*")},{"Access-Control-Allow-Credentials","true"}} });
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button,new(){Name="Open Owner shop",Exact=true}).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Today", Level = 1, Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Analytics", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Analytics",Exact=true})).ToBeVisibleAsync();
        await Expect(page).ToHaveURLAsync(host.Url + "/analytics");
        await Expect(page.Locator(".admin-detail-tabs button")).ToHaveCountAsync(4);
        var slider = page.GetByRole(AriaRole.Slider, new() { Name = "Select trend date" });
        var period = page.GetByLabel("Period", new() { Exact = true });
        await period.SelectOptionAsync("today");
        var today = Calculations.Today();
        await Expect(page.Locator(".performance-range")).ToHaveTextAsync($"{today:MMM d, yyyy} · Synced records");
        await Expect(page.Locator(".trend-range")).ToHaveTextAsync(
            $"Chart period · {today.AddDays(-6):MMM d, yyyy} – {today:MMM d, yyyy}");
        Assert.Contains(reportRequests, request => request.AbsolutePath.EndsWith("/reports/items")
            && request.Query == $"?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        await Expect(page.Locator(".admin-financials")).ToHaveCountAsync(0);
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(7);
        await slider.PressAsync("Home");
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱0.00");
        await slider.PressAsync("End");
        await slider.PressAsync("ArrowLeft");
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱65.00");
        await page.Locator(".trend-hit").Last.TapAsync();
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱35.00");

        await period.SelectOptionAsync("custom");
        var fromDate = page.GetByLabel("From date");
        var toDate = page.GetByLabel("To date");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Apply range" })).ToHaveCountAsync(0);
        await Expect(toDate).ToHaveAttributeAsync("min", today.ToString("yyyy-MM-dd"));
        int requestsBeforeInvalidDates = paths.Count(path => path.EndsWith("/analytics"));
        await toDate.FillAsync(today.AddDays(-1).ToString("yyyy-MM-dd"));
        await Expect(page.GetByText("To date must be on or after the from date.")).ToBeVisibleAsync();
        await toDate.FillAsync(today.AddDays(1).ToString("yyyy-MM-dd"));
        await Expect(page.GetByText("Choose today or earlier.")).ToBeVisibleAsync();
        await toDate.FillAsync("");
        await Expect(toDate).ToHaveAttributeAsync("aria-invalid", "true");
        Assert.Equal(requestsBeforeInvalidDates, paths.Count(path => path.EndsWith("/analytics")));

        await fromDate.FillAsync(today.AddDays(-1).ToString("yyyy-MM-dd"));
        await toDate.FillAsync(today.AddDays(-1).ToString("yyyy-MM-dd"));
        var yesterday = today.AddDays(-1);
        await Expect(page.Locator(".performance-range")).ToHaveTextAsync($"{yesterday:MMM d, yyyy} · Synced records");
        await Expect(page.Locator(".trend-range")).ToHaveTextAsync(
            $"Chart period · {yesterday.AddDays(-6):MMM d, yyyy} – {yesterday:MMM d, yyyy}");
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(7);
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱65.00");

        await period.SelectOptionAsync("last30");
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(30);
        await period.SelectOptionAsync("yesterday");
        await Expect(page.Locator(".performance-range")).ToHaveTextAsync($"{yesterday:MMM d, yyyy} · Synced records");
        await Expect(page.Locator(".trend-range")).ToHaveTextAsync(
            $"Chart period · {yesterday.AddDays(-6):MMM d, yyyy} – {yesterday:MMM d, yyyy}");
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(7);

        await period.SelectOptionAsync("last30");
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(30);

        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Sales & profit",Exact=true})).ToBeVisibleAsync();
        await Expect(page.Locator(".bottom-nav a[href='/analytics'] svg")).ToBeVisibleAsync();
        foreach(var tab in new[]{"Sales","Expenses","Items","Summary"}) {
            await page.Locator(".admin-detail-tabs").GetByRole(AriaRole.Button,new(){Name=tab,Exact=true}).ClickAsync();
            await Expect(page.GetByText("Loading business records…",new(){Exact=true})).ToHaveCountAsync(0);
        }
        Assert.All(paths,path=>Assert.StartsWith($"/api/businesses/{business.Id}/overview/",path));
        foreach(var width in new[]{320,390,768,1366}) {
            await page.SetViewportSizeAsync(width,900);
            var nav = page.GetByRole(AriaRole.Navigation, new() { Name = width >= 980 ? "Main navigation" : "Mobile navigation" });
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Today", Exact = true })).ToHaveAttributeAsync("href", "/dashboard");
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Analytics", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
            Assert.True(await page.EvaluateAsync<bool>("document.querySelector('.performance-filter-panel').getBoundingClientRect().top >= document.querySelector('.page-heading').getBoundingClientRect().bottom"));
        }
        await page.GotoAsync(host.Url + "/business-overview");
        await Expect(page).ToHaveURLAsync(host.Url + "/analytics");
        await context.SetOfflineAsync(true);
        await Expect(page.GetByText("Connect to load business financial data. It is not saved offline.",new(){Exact=true})).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Sales & profit",Exact=true})).ToHaveCountAsync(0);
    }
}


