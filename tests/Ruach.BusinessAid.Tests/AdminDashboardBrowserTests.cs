using System.Text.Json;
using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task AdminDashboardMatchesResponsiveDesignAndReadsRecordsWithoutSwitchingWorkspace()
    {
        await using var host=await Preview.Start();
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true,Channel=Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL")});
        await using var context=await browser.NewContextAsync(new(){HasTouch=true,ViewportSize=new(){Width=390,Height=844}});
        var admin=new AppUser(Guid.NewGuid(),"Platform admin","admin@example.invalid",null){IsAdmin=true};
        var owner=new AdminUser(Guid.NewGuid(),"Ana Reyes","ana@example.invalid","+639181234567",DateTimeOffset.UtcNow,1);
        var business=new Business{Id=Guid.NewGuid(),OwnerUid=owner.Uid,Name="Cebu Kitchen",DefaultLocation="Cebu City",CreatedAt=DateTimeOffset.UtcNow};
        var pending=new BusinessRequest{Id=Guid.NewGuid(),OwnerUid=owner.Uid,Name="Amihan Kitchen",DefaultLocation="Cebu City",CreatedAt=DateTimeOffset.UtcNow.AddDays(-2)};
        var metadata=new AdminOverview([owner],[business],[pending],[]);
        var today=Calculations.Today().ToString("yyyy-MM-dd");
        var analytics=new AdminAnalytics(new(28465000,16390000,3460000,12075000,8615000,120,450),
            [new("2026-10-01",1000000,600000,400000,100000,300000),new("2026-10-08",2200000,900000,1300000,200000,1100000),new("2026-10-15",1800000,1100000,700000,400000,300000),new("2026-10-22",2800000,1200000,1600000,300000,1300000)],
            [new(business.Id,business.Name,10000000,5800000,1200000,3000000)]);
        var records=new AppData{Business=business,Sales=[new(){Id=Guid.NewGuid(),BusinessId=business.Id,SaleDate=today,CreatedAt=DateTimeOffset.UtcNow,Location="Cebu",TotalItems=2,TotalRevenueCentavos=7000,TotalCostCentavos=4000,TotalProfitCentavos=3000,Lines=[new(){Id=Guid.NewGuid(),ItemName="Buko Juice",Quantity=2,UnitPriceCentavos=3500,UnitCostCentavos=2000,LineRevenueCentavos=7000,LineCostCentavos=4000}]}]};
        await MockApi(context,admin,new());
        var requested=new List<string>();
        await context.RouteAsync("**/api/admin**",async route=>{
            var path=new Uri(route.Request.Url).AbsolutePath;requested.Add(route.Request.Url);
            object body=path switch{
                "/api/admin"=>metadata,
                _ when path.Contains("/directory/")=>new AdminDirectory{Users=[owner],Businesses=[business],Total=1},
                "/api/admin/analytics" when route.Request.Url.Contains("business=") || route.Request.Url.Contains("owner=")=>new AdminAnalytics(new(10000000,5800000,1200000,4200000,3000000,50,200),[new(today,10000000,5800000,4200000,1200000,3000000)],analytics.TopBusinesses),
                "/api/admin/analytics"=>analytics,
                _ when path.EndsWith("/records/sales")=>new AdminRecords(records,26,1,25),
                _ when path.EndsWith("/records/expenses")=>new AdminRecords(new(){Expenses=[new(){Description="Transport fare",Category="Transport",AmountCentavos=5000,ExpenseDate=today}]},1,1,25),
                _ when path.EndsWith("/records/items")=>new AdminRecords(new(){Items=[new(){Name="Buko Juice",IsActive=true,CurrentQuantity=0,MinimumQuantity=2,SellingPriceCentavos=3500,UnitCostCentavos=2000}]},1,1,25),
                _ when path.EndsWith("/records/receipts") || path.EndsWith("/records/adjustments")=>new AdminRecords(new(),0,1,25),
                _ when path.EndsWith("/memberships")=>new List<AdminMembership>{new(business.Id,business.Name,"Owner")},
                _ when path.EndsWith("/members")=>new List<AdminMember>{new(owner.Uid,owner.DisplayName,owner.Email,"Owner")},
                _ when path.EndsWith("/reports/items")=>new List<ItemReport>{new(Guid.NewGuid(),"Buko Juice",2,7000,4000,3000)},
                _=>new List<ExpenseReport>()
            };
            await route.FulfillAsync(new(){Status=200,ContentType="application/json",Headers=ApiHeaders(route),Body=JsonSerializer.Serialize(body,Wire.Json)});
        });
        var page=await context.NewPageAsync();var errors=new List<string>();page.PageError+=(_,e)=>errors.Add(e);
        await page.GotoAsync(host.Url+"/admin");
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Overview",Exact=true})).ToBeVisibleAsync();
        await Expect(page.Locator(".net-profit")).ToContainTextAsync("86,150.00");
        foreach(var width in new[]{320,390,768,1366}){
            await page.SetViewportSizeAsync(width,900);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth+1"));
            Assert.True(await page.Locator(".pending-panel").EvaluateAsync<bool>("e => e.getBoundingClientRect().top < document.querySelector('.admin-performance').getBoundingClientRect().top"));
            await page.ScreenshotAsync(new(){Path=Path.Combine(host.Root,"artifacts","browser",$"admin-overview-{width}.png"),FullPage=true});
            await page.Locator(".admin-charts > .card").First.ScreenshotAsync(new(){Path=Path.Combine(host.Root,"artifacts","browser",$"admin-chart-{width}.png")});
        }
        await page.SetViewportSizeAsync(390,844);
        await page.GetByLabel("Period",new(){Exact=true}).SelectOptionAsync("custom");
        await page.GetByLabel("From date",new(){Exact=true}).FillAsync("2026-10-22");
        await page.GetByLabel("To date",new(){Exact=true}).FillAsync("2026-10-01");
        await Expect(page.GetByRole(AriaRole.Button,new(){Name="Apply range",Exact=true})).ToBeDisabledAsync();
        await Expect(page.GetByText("To date must be on or after the from date.")).ToBeVisibleAsync();
        await page.GetByLabel("Period",new(){Exact=true}).SelectOptionAsync("month");
        await page.GetByRole(AriaRole.Button,new(){Name="Businesses",Exact=true}).TapAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="View business",Exact=true}).TapAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Cebu Kitchen",Exact=true})).ToBeVisibleAsync();
        await Expect(page.Locator(".admin-detail-tabs button")).ToHaveCountAsync(4);
        await Expect(page.Locator(".business-detail .admin-financials")).ToHaveCountAsync(0);
        await page.GetByText("More records",new(){Exact=true}).TapAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="Members",Exact=true}).TapAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Business members"})).ToBeVisibleAsync();

        var businessPeriod = page.GetByLabel("Period",new(){Exact=true});
        var currentDay = Calculations.Today();
        var yesterday = currentDay.AddDays(-1);
        await businessPeriod.SelectOptionAsync("yesterday");
        await Expect(page.Locator(".performance-range")).ToHaveTextAsync($"{yesterday:MMM d, yyyy} · Synced records");
        await Expect(page.Locator(".trend-range")).ToHaveTextAsync(
            $"Chart period · {yesterday.AddDays(-6):MMM d, yyyy} – {yesterday:MMM d, yyyy}");
        await Expect(page.Locator(".trend-hit")).ToHaveCountAsync(7);
        Assert.Contains(requested, url => url.Contains($"/api/admin/analytics?from={yesterday.AddDays(-6):yyyy-MM-dd}&to={yesterday:yyyy-MM-dd}&business={business.Id}"));
        await businessPeriod.SelectOptionAsync("custom");
        await Expect(page.GetByRole(AriaRole.Button,new(){Name="Apply range",Exact=true})).ToHaveCountAsync(0);
        await page.GetByLabel("To date",new(){Exact=true}).FillAsync(currentDay.ToString("yyyy-MM-dd"));
        await Expect(page.Locator(".performance-range")).ToHaveTextAsync(
            $"{yesterday:MMM d, yyyy} – {currentDay:MMM d, yyyy} · Synced records");
        await page.GetByRole(AriaRole.Slider,new(){Name="Select trend date"}).PressAsync("Home");
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱0.00");
        await page.Locator(".trend-hit").Last.TapAsync();
        await Expect(page.Locator(".trend-selection")).ToContainTextAsync("₱100,000.00");
        await page.Locator(".business-detail").ScreenshotAsync(new(){Path=Path.Combine(host.Root,"artifacts","browser","admin-business-analytics-mobile.png")});
        await page.GetByRole(AriaRole.Button,new(){Name="Sales",Exact=true}).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button,new(){Name="Sales",Exact=true})).ToHaveAttributeAsync("aria-pressed","true");
        Assert.Contains(requested, url => url.Contains($"/records/sales?from={yesterday:yyyy-MM-dd}&to={currentDay:yyyy-MM-dd}"));
        await page.Locator(".admin-sale summary").ClickAsync();
        await Expect(page.GetByText("Buko Juice",new(){Exact=true})).ToBeVisibleAsync();
        await page.EvaluateAsync("window.scrollTo(0,0)");
        await page.ScreenshotAsync(new(){Path=Path.Combine(host.Root,"artifacts","browser","admin-business-mobile.png"),FullPage=true});
        await page.GetByRole(AriaRole.Button,new(){Name="Next",Exact=true}).ClickAsync();
        Assert.Contains(requested,x=>x.Contains("page=2"));
        await page.GetByRole(AriaRole.Button,new(){Name="Expenses",Exact=true}).ClickAsync();
        await Expect(page.GetByText("Transport fare",new(){Exact=true})).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="Items",Exact=true}).ClickAsync();
        await Expect(page.GetByText("Low stock",new(){Exact=true})).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="Users",Exact=true}).ClickAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="View user",Exact=true}).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Owned-business performance",Exact=true})).ToBeVisibleAsync();
        Assert.Contains(requested,x=>x.Contains("owner="+owner.Uid));
        await context.SetOfflineAsync(true);
        await Expect(page.GetByText("Connect to load admin financial data. It is not saved offline.",new(){Exact=true})).ToBeVisibleAsync();
        Assert.Empty(errors);
        // Only account identity metadata may be persisted; operational admin records must never reach IndexedDB.
        var persisted=await page.EvaluateAsync<string>("async () => { const db=await new Promise((ok,fail)=>{const r=indexedDB.open('mashal-sql-v2');r.onsuccess=()=>ok(r.result);r.onerror=fail}); const rows=[];for(const name of db.objectStoreNames){rows.push(await new Promise(ok=>{const r=db.transaction(name).objectStore(name).getAll();r.onsuccess=()=>ok(r.result)}))}db.close();return JSON.stringify(rows);}");
        Assert.DoesNotContain("Buko Juice",persisted);Assert.DoesNotContain("Transport fare",persisted);
    }
}
