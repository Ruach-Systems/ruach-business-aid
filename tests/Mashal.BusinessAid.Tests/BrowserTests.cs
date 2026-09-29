using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Playwright;
using Mashal.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Mashal.BusinessAid.Tests;

public sealed class BrowserFactAttribute : FactAttribute
{
    public BrowserFactAttribute() { if (Environment.GetEnvironmentVariable("MASHAL_BROWSER_TESTS") != "true") Skip = "Set MASHAL_BROWSER_TESTS=true after publishing the PWA and installing Playwright browsers."; }
}
[Trait("Category", "Browser")]
public partial class BrowserTests
{
    [BrowserFact]
    public async Task PublishedPwaPreservesDataAcrossOfflineReloadsTabsAndUpdates()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var account = new AppUser(Guid.NewGuid(), "Browser tester", "browser@example.invalid", null) { PhoneNumber="+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = account.Uid, Name = "Browser test shop", DefaultLocation = "Main", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var item = new Item { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Buko Juice", IsActive = true, SellingPriceCentavos = 3500, UnitCostCentavos = 1630, Version = [0, 0, 0, 0, 0, 0, 0, 1], CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var data = new AppData { Business = business, Items = [item] };
        await MockApi(context, account, data);
        var page = await context.NewPageAsync();
        var consoleErrors = new List<string>(); page.PageError += (_, error) => consoleErrors.Add(error);
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name="Open Browser test shop", Exact=true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");
        // Seed a legacy model without modelVersion. The simplified release must discard it.
        var expenseId = Guid.NewGuid();
        var operation = new { id = Guid.NewGuid(), entityId = expenseId, type = "saveExpense", expectedVersion = (string?)null, payload = new { id = expenseId, description = "Legacy fare", category = "Transportation", amountCentavos = 2500, expenseDate = "2026-09-15" }, projections = Array.Empty<object>(), dependsOnPending = false, prepared = true };
        var oldState = JsonSerializer.Serialize(new { user = account, workspaceId=business.Id, data, serverData = data, cursor = 0, outbox = new[] { operation } }, Wire.Json);
        await page.EvaluateAsync("async args => { await mashalStorage.write('accounts',args.uid,args.state); }", new { uid = $"{account.Uid}:{business.Id}", state = oldState });
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        var preserved = await ReadState(page, account.Uid);
        Assert.Equal(DataModel.CurrentVersion, preserved.GetProperty("modelVersion").GetInt32());
        Assert.Equal(0, preserved.GetProperty("outbox").GetArrayLength());

        foreach (var width in new[] { 320, 390, 768, 1366 })
        {
            await page.SetViewportSizeAsync(width, 900);
            foreach (var route in new[] { "dashboard", "sales", "expenses", "items", "items/new", "stock/add", "stock/adjust", "more", "settings", "reports" })
            {
                await page.GotoAsync(host.Url + "/" + route);
                await Expect(page.Locator("main h1")).ToBeVisibleAsync();
                Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"), $"Overflow at {route}, {width}px");
            }
            Directory.CreateDirectory(Path.Combine(host.Root, "artifacts", "browser"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"blazor-reports-{width}.png"), FullPage = true });
        }
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(host.Url + "/items");
        await page.Locator("#item-search").FocusAsync();
        await page.WaitForTimeoutAsync(150);
        Assert.Equal("none", await page.Locator("#item-search").EvaluateAsync<string>("element => getComputedStyle(element).boxShadow"));
        var searchFocusRing = await page.Locator(".search-box").EvaluateAsync<string>("element => getComputedStyle(element).boxShadow");
        Assert.Contains("2px", searchFocusRing);
        Assert.DoesNotContain("6px", searchFocusRing);
        await page.SetViewportSizeAsync(390, 844);
        await context.SetOfflineAsync(true);
        await page.GotoAsync(host.Url + "/sales");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "What sold today?" })).ToBeVisibleAsync();
        var saleQuantity = page.GetByLabel("Buko Juice quantity sold", new() { Exact = true });
        await saleQuantity.FillAsync("2");
        await page.WaitForTimeoutAsync(150);
        var saleQuantityFocusRing = await saleQuantity.EvaluateAsync<string>("element => getComputedStyle(element).boxShadow");
        Assert.Contains("2px", saleQuantityFocusRing);
        Assert.DoesNotContain("6px", saleQuantityFocusRing);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save 2 items", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Sale saved on this device.", new() { Exact = true })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "What sold today?" })).ToBeVisibleAsync();
        var after = await ReadState(page, account.Uid);
        Assert.Equal(1, after.GetProperty("data").GetProperty("sales").GetArrayLength());
        Assert.Equal(7000, after.GetProperty("data").GetProperty("sales")[0].GetProperty("totalRevenueCentavos").GetInt64());
        await page.GotoAsync(host.Url + "/reports");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Reports require an online account" })).ToBeVisibleAsync();
        // Two independent tabs append without replacing either outbox.
        var second = await context.NewPageAsync();
        await second.GotoAsync(host.Url + "/expenses"); await page.GotoAsync(host.Url + "/expenses");
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Tab one");
        await second.GetByLabel("Description", new() { Exact = true }).FillAsync("Tab two");
        await page.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("10");
        await second.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("20");
        await Task.WhenAll(page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync(), second.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync());
        await Expect(page.GetByText("Tab one", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(second.GetByText("Tab two", new() { Exact = true })).ToBeVisibleAsync();
        after = await ReadState(page, account.Uid);
        Assert.Equal(2, after.GetProperty("data").GetProperty("expenses").GetArrayLength());
        var queued = after.GetProperty("outbox").GetArrayLength();
        await second.CloseAsync();
        await context.SetOfflineAsync(false);
        host.Release = 2;
        await page.EvaluateAsync("async () => { const r=await navigator.serviceWorker.getRegistration(); await r.update(); }");
        await page.WaitForFunctionAsync("async () => (await caches.keys()).some(x=>x.endsWith('-test2'))");
        await Expect(page.Locator("main h1")).ToBeVisibleAsync();
        after = await ReadState(page, account.Uid);
        Assert.Equal(queued, after.GetProperty("outbox").GetArrayLength());
        Assert.False(await page.EvaluateAsync<bool>("async () => { for(const name of await caches.keys()){for(const request of await (await caches.open(name)).keys()){if(new URL(request.url).pathname.startsWith('/api/')) return true;}} return false;}"));
        Assert.Empty(consoleErrors);
    }

    [BrowserFact]
    public async Task OnboardingAndSimpleItemWorkflowWorkWithoutDemoBypass()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var account = new AppUser(Guid.NewGuid(), "Owner", "onboarding@example.invalid", null);
        var onboardingData=new AppData();
        await MockApi(context, account, onboardingData);
        var page = await context.NewPageAsync(); await page.GotoAsync(host.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Request your first business" })).ToBeVisibleAsync();
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Reset sign-in state" }).CountAsync());
        await page.GetByLabel("Business name", new() { Exact = true }).FillAsync("Form verification");
        await page.GetByLabel("Philippine mobile number",new(){Exact=true}).FillAsync("09171234567");
        await page.GetByRole(AriaRole.Button, new() { Name = "Submit request", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Pending",new(){Exact=true})).ToBeVisibleAsync();
        Assert.Null(onboardingData.Business);
        // Simulate a separately approved server workspace; onboarding itself created none.
        onboardingData.Business=new Business {Id=Guid.NewGuid(),OwnerUid=account.Uid,Name="Form verification",DefaultLocation="Main"};
        await page.GetByRole(AriaRole.Button,new(){Name="Refresh status",Exact=true}).ClickAsync();
        await page.GetByRole(AriaRole.Button,new(){Name="Open Form verification",Exact=true}).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/items/new");
        await page.GetByLabel("Item name", new() { Exact = true }).FillAsync("Bread");
        await page.GetByLabel("Selling price (₱)", new() { Exact = true }).FillAsync("25");
        await page.GetByLabel("Unit cost (₱)", new() { Exact = true }).FillAsync("10");
        await CheckResponsiveForm(page, host.Root, "item-editor");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save item", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Items", Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/stock/add");
        var state = await ReadState(page, account.Uid);
        var itemId = state.GetProperty("data").GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="Bread").GetProperty("id").GetString()!;
        await page.GetByLabel("Item", new() { Exact = true }).SelectOptionAsync(itemId);
        await page.GetByLabel("Quantity received", new() { Exact = true }).FillAsync("2");
        await CheckResponsiveForm(page, host.Root, "stock-receipt");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save stock", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Items", Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/sales");
        await page.GetByLabel("Bread quantity sold", new() { Exact = true }).FillAsync("3");
        await Expect(page.GetByText("One or more items will have negative stock.",new(){Exact=false})).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save 3 items", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Sale saved on this device.", new() { Exact = true })).ToBeVisibleAsync();
        state = await ReadState(page, account.Uid);
        var saleId = state.GetProperty("data").GetProperty("sales").EnumerateArray().Single().GetProperty("id").GetString()!;
        await page.GotoAsync(host.Url + "/dashboard");
        await Expect(page.Locator($"a[href='/sales/{saleId}']")).ToBeVisibleAsync();
        await page.SetViewportSizeAsync(390, 600);
        await page.GotoAsync(host.Url + "/sales");
        var saleHistoryLink = page.Locator($"a[href='/sales/{saleId}']");
        await saleHistoryLink.ScrollIntoViewIfNeededAsync();
        Assert.True(await page.EvaluateAsync<int>("() => document.querySelector('.app-shell').scrollTop") > 0, "Sales history did not create the scrolled navigation state");
        await saleHistoryLink.ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Sale details", Exact = true })).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.app-shell')?.scrollTop === 0");
        await Expect(page.GetByText("Today · 3 items", new() { Exact = true })).ToBeVisibleAsync();
        var saleLine = page.Locator("article[aria-label='Bread sale line']");
        await Expect(saleLine).ToContainTextAsync("3 × ₱25.00 each");
        await Expect(saleLine).ToContainTextAsync("₱75.00");
        await Expect(saleLine).ToContainTextAsync("₱30.00");
        await Expect(saleLine).ToContainTextAsync("₱45.00");
        var saleTotals = page.GetByLabel("This sale totals", new() { Exact = true });
        await Expect(saleTotals).ToContainTextAsync("Revenue₱75.00");
        await Expect(saleTotals).ToContainTextAsync("Item cost₱30.00");
        await Expect(saleTotals).ToContainTextAsync("Gross profit₱45.00");
        await page.SetViewportSizeAsync(320, 900);
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"), "Sale detail overflows at 320px");
        Directory.CreateDirectory(Path.Combine(host.Root, "artifacts", "browser"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", "sale-detail-320.png"), FullPage = true });
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(host.Url + "/items/" + itemId);
        await Expect(page.GetByText("Stock is negative",new(){Exact=false})).ToBeVisibleAsync();
        state = await ReadState(page, account.Uid);
        Assert.Equal(-1, state.GetProperty("data").GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="Bread").GetProperty("currentQuantity").GetInt64());
        await page.GotoAsync(host.Url + "/expenses");
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Delivery fare");
        await page.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Delivery fare", new() { Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/sales/" + saleId);
        var dayTotals = page.GetByLabel("Sale date totals", new() { Exact = true });
        Assert.Equal("₱10.00", await dayTotals.Locator(".day-profit-grid > div:nth-child(3) strong").TextContentAsync());
        Assert.Equal("₱35.00", await dayTotals.Locator(".day-profit-grid > div:nth-child(4) strong").TextContentAsync());
        await page.GotoAsync(host.Url + "/reports");
        await Expect(page.GetByText("Item performance", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(new[] { "Today", "This week", "This month", "Custom range" }, await page.Locator("#report-period option").AllTextContentsAsync());
        await page.GetByLabel("Period", new() { Exact = true }).SelectOptionAsync("custom");
        await page.SetViewportSizeAsync(320, 900);
        Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"), "Custom report range overflows at 320px");
        await page.GetByLabel("From", new() { Exact = true }).FillAsync("2026-09-01");
        await page.GetByLabel("To", new() { Exact = true }).FillAsync("2026-09-29");
        await page.GetByRole(AriaRole.Button, new() { Name = "View reports", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Item performance", new() { Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/products");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Items", Exact = true })).ToBeVisibleAsync();
    }

    private static async Task CheckResponsiveForm(IPage page, string root, string name)
    {
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "browser"));
        foreach (var width in new[] { 320, 390, 436, 482, 768, 1366 })
        {
            await page.SetViewportSizeAsync(width, 912);
            var overflowing = await page.EvaluateAsync<string[]>("""
                () => Array.from(document.querySelectorAll('main input, main select, main button, main strong, main small'))
                  .filter(e => { const r=e.getBoundingClientRect(); return r.width > 0 && (r.right > innerWidth + 1 || r.left < -1 || e.scrollWidth > e.clientWidth + 2); })
                  .map(e => e.outerHTML)
                """);
            Assert.True(overflowing.Length == 0, $"{name} at {width}px: {string.Join("; ", overflowing)}");
            await page.Locator("main form").ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(root, "artifacts", "browser", $"{name}-{width}.png") });
        }
    }

    private static async Task<JsonElement> ReadState(IPage page, Guid user)
    {
        var json = await page.EvaluateAsync<string>("async uid => { const id=JSON.parse(await mashalStorage.read('meta','selected:'+uid)); return mashalStorage.read('accounts',uid+':'+id); }", user.ToString());
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    private static Task MockApi(IBrowserContext context, AppUser account, AppData data, List<AppData>? additional = null)
    {
        var requests=new List<BusinessRequest>();
        return context.RouteAsync("**/api/**", async route => {
        var path = new Uri(route.Request.Url).AbsolutePath;
        if(route.Request.Method=="POST" && path=="/api/account/phone") {
            var input=JsonSerializer.Deserialize<PhoneInput>(route.Request.PostData!,Wire.Json)!;
            account=account with {PhoneNumber=PhilippinePhone.Normalize(input.PhoneNumber)};
        }
        if(route.Request.Method=="POST" && path=="/api/account/requests") {
            var input=JsonSerializer.Deserialize<BusinessRequestInput>(route.Request.PostData!,Wire.Json)!;
            account=account with {PhoneNumber=PhilippinePhone.Normalize(input.PhoneNumber ?? account.PhoneNumber)};
            requests.Add(new(){Id=input.Id,OwnerUid=account.Uid,Name=input.Name,DefaultLocation=input.DefaultLocation,CreatedAt=DateTimeOffset.UtcNow});
        }
        var all=new[]{data}.Concat(additional ?? []).ToList();
        var businessId=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(route.Request.Url).Query).GetValueOrDefault("businessId").ToString();
        var selected=all.Find(x=>x.Business?.Id.ToString()==businessId) ?? data;
        object response = path switch
        {
            "/api/auth/session" => account,
            "/api/account" => new AccountOverview(account,all.Where(x=>x.Business is not null).Select(x=>x.Business!).ToList(),requests),
            "/api/bootstrap" => new BootstrapResult(DataModel.CurrentVersion, account, selected, 0),
            "/api/auth/antiforgery" => new { token = "fixture" },
            "/api/sync/pull" => new PullResult(DataModel.CurrentVersion, 0, [], false),
            "/api/sync/push" => new { code = "retry", title = "Test connection interrupted" },
            "/api/reports/summary" => new FinancialSummary(7000, 3260, 1000, 3740, 2740, 1, 2),
            _ => Array.Empty<object>()
        };
        var headers = new Dictionary<string, string> { { "Access-Control-Allow-Origin", route.Request.Headers.GetValueOrDefault("origin", "*") }, { "Access-Control-Allow-Credentials", "true" }, { "Access-Control-Allow-Headers", "Content-Type,X-CSRF-TOKEN" }, { "Access-Control-Allow-Methods", "GET,POST,OPTIONS" } };
        await route.FulfillAsync(new() { Status = path == "/api/sync/push" && route.Request.Method != "OPTIONS" ? 503 : 200, ContentType = "application/json", Body = JsonSerializer.Serialize(response, Wire.Json), Headers = headers });
        });
    }

    private sealed class Preview : IAsyncDisposable
    {
        private WebApplication app = default!;
        public string Url = string.Empty;
        public string Root = string.Empty;
        public int Release = 1;
        public static async Task<Preview> Start()
        {
            var root = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(root, "Mashal.BusinessAid.slnx"))) root = Directory.GetParent(root)?.FullName ?? throw new InvalidOperationException("Solution root not found.");
            var webroot = Environment.GetEnvironmentVariable("MASHAL_PWA_ROOT") ?? Path.Combine(root, "artifacts", "pwa", "wwwroot");
            if (!File.Exists(Path.Combine(webroot, "index.html"))) throw new InvalidOperationException("Publish the Blazor client to artifacts/pwa before browser tests.");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webroot, ContentRootPath = root, EnvironmentName = "Development" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var instance = new Preview { Root = root, app = builder.Build() };
            instance.app.Use(async (ctx, next) =>
            {
                ctx.Response.Headers.CacheControl = "no-cache";
                if (ctx.Request.Path == "/sw.js") { ctx.Response.ContentType = "text/javascript"; await ctx.Response.WriteAsync(await File.ReadAllTextAsync(Path.Combine(webroot, "sw.js")) + "\n// test release " + instance.Release); return; }
                if (ctx.Request.Path == "/service-worker-assets.js") { ctx.Response.ContentType = "text/javascript"; await ctx.Response.WriteAsync(await File.ReadAllTextAsync(Path.Combine(webroot, "service-worker-assets.js")) + $"\nself.assetsManifest.version += '-test{instance.Release}';"); return; }
                await next();
            });
            var types = new FileExtensionContentTypeProvider(); types.Mappings[".wasm"] = "application/wasm"; types.Mappings[".dat"] = "application/octet-stream"; types.Mappings[".webmanifest"] = "application/manifest+json";
            instance.app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = types, ServeUnknownFileTypes = true });
            instance.app.MapFallbackToFile("index.html");
            await instance.app.StartAsync(); instance.Url = instance.app.Urls.Single(); return instance;
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}
