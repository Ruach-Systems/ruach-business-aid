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
        await using var context = await browser.NewContextAsync(new() { ViewportSize=new() { Width=390, Height=844 } });
        var user = new AppUser(Guid.NewGuid(),"Owner","owner@example.invalid",null) { PhoneNumber="+639181234567" };
        var business = new Business { Id=Guid.NewGuid(),OwnerUid=user.Uid,Name="Owner shop",DefaultLocation="Cebu",CreatedAt=DateTimeOffset.UtcNow };
        await MockApi(context,user,new() { Business=business });
        var paths = new List<string>();
        await context.RouteAsync("**/api/businesses/**",async route => {
            var path = new Uri(route.Request.Url).AbsolutePath; paths.Add(path);
            object body = path.EndsWith("/analytics") ? new AdminAnalytics(new(10000,6000,1000,4000,3000,1,2),[new(Calculations.Today().ToString("yyyy-MM-dd"),10000,6000,4000,1000,3000)],[])
                : path.EndsWith("/members") ? new List<AdminMember> { new(user.Uid,user.DisplayName,user.Email,"Owner") }
                : path.Contains("/records/") ? new AdminRecords(new(),0,1,25)
                : path.EndsWith("/items") ? new List<ItemReport>() : (object)new List<ExpenseReport>();
            await route.FulfillAsync(new() { Status=200,ContentType="application/json",Body=JsonSerializer.Serialize(body,Wire.Json),Headers=new Dictionary<string,string>{{"Access-Control-Allow-Origin",route.Request.Headers.GetValueOrDefault("origin","*")},{"Access-Control-Allow-Credentials","true"}} });
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button,new(){Name="Open Owner shop",Exact=true}).ClickAsync();
        await page.Locator(".bottom-nav a[href='/business-overview']").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Business Overview",Exact=true})).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Sales & profit",Exact=true})).ToBeVisibleAsync();
        await Expect(page.Locator(".bottom-nav a[href='/business-overview'] svg")).ToBeVisibleAsync();
        foreach(var tab in new[]{"Sales","Expenses","Items","Receipts","Adjustments","Members","Summary"}) {
            await page.Locator(".admin-detail-tabs").GetByRole(AriaRole.Button,new(){Name=tab,Exact=true}).ClickAsync();
            await Expect(page.GetByText("Loading business records…",new(){Exact=true})).ToHaveCountAsync(0);
        }
        Assert.All(paths,path=>Assert.StartsWith($"/api/businesses/{business.Id}/overview/",path));
        foreach(var width in new[]{320,390,768,1366}) {
            await page.SetViewportSizeAsync(width,900);
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
            Assert.True(await page.EvaluateAsync<bool>("document.querySelector('.admin-profile').getBoundingClientRect().top >= document.querySelector('.page-heading').getBoundingClientRect().bottom"));
        }
        await context.SetOfflineAsync(true);
        await Expect(page.GetByText("Connect to load business financial data. It is not saved offline.",new(){Exact=true})).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Sales & profit",Exact=true})).ToHaveCountAsync(0);
    }
}


