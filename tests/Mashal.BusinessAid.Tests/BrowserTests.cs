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
public class BrowserTests
{
    [BrowserFact]
    public async Task PublishedPwaPreservesDataAcrossOfflineReloadsTabsAndUpdates()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var account = new AppUser(Guid.NewGuid(), "Browser tester", "browser@example.invalid", null);
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = account.Uid, Name = "Browser test shop", DefaultLocation = "Main", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Buko Juice", InventoryMode = "untracked", IsActive = true, SellingPriceCentavos = 3500, ManualCostCentavos = 1630, Version = [0, 0, 0, 0, 0, 0, 0, 1], CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var data = new AppData { Business = business, Products = [product] };
        await MockApi(context, account, data);
        var page = await context.NewPageAsync();
        var consoleErrors = new List<string>(); page.PageError += (_, error) => consoleErrors.Add(error);
        await page.GotoAsync(host.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");
        // Seed exactly the former JavaScript shape: no revision property and a prepared outbox request.
        var expenseId = Guid.NewGuid();
        var operation = new { id = Guid.NewGuid(), entityId = expenseId, type = "saveExpense", expectedVersion = (string?)null, payload = new { id = expenseId, description = "Legacy fare", category = "Transportation", amountCentavos = 2500, expenseDate = "2026-09-15" }, projections = Array.Empty<object>(), dependsOnPending = false, prepared = true };
        var oldState = JsonSerializer.Serialize(new { user = account, data, serverData = data, cursor = 0, outbox = new[] { operation } }, Wire.Json);
        await page.EvaluateAsync("async args => { await mashalStorage.write('accounts',args.uid,args.state); }", new { uid = account.Uid.ToString(), state = oldState });
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        var preserved = await ReadState(page, account.Uid);
        Assert.Equal(operation.id, preserved.GetProperty("outbox")[0].GetProperty("id").GetGuid());
        Assert.True(preserved.GetProperty("outbox")[0].GetProperty("prepared").GetBoolean());

        foreach (var width in new[] { 320, 390, 768, 1366 })
        {
            await page.SetViewportSizeAsync(width, 900);
            foreach (var route in new[] { "dashboard", "sales", "expenses", "inventory", "inventory/new", "stock/add", "stock/adjust", "products", "products/new", "production", "production/new", "more", "settings", "reports" })
            {
                await page.GotoAsync(host.Url + "/" + route);
                await Expect(page.Locator("main h1")).ToBeVisibleAsync();
                Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"), $"Overflow at {route}, {width}px");
            }
            Directory.CreateDirectory(Path.Combine(host.Root, "artifacts", "browser"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"blazor-reports-{width}.png"), FullPage = true });
        }
        await page.SetViewportSizeAsync(390, 844);
        await context.SetOfflineAsync(true);
        await page.GotoAsync(host.Url + "/sales");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "What sold today?" })).ToBeVisibleAsync();
        await page.GetByLabel("Buko Juice quantity sold", new() { Exact = true }).FillAsync("2");
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
    public async Task ProductionOnboardingAndOperationalFormsWorkWithoutDemoBypass()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var account = new AppUser(Guid.NewGuid(), "Owner", "onboarding@example.invalid", null);
        await MockApi(context, account, new AppData());
        var page = await context.NewPageAsync(); await page.GotoAsync(host.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Create your business" })).ToBeVisibleAsync();
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Reset sign-in state" }).CountAsync());
        await page.GetByLabel("Business name", new() { Exact = true }).FillAsync("Form verification");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create business", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/inventory/new");
        await page.GetByLabel("Item name", new() { Exact = true }).FillAsync("Flour");
        await page.GetByLabel("Base unit", new() { Exact = true }).SelectOptionAsync("g");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save item", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Inventory", Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/stock/add");
        var state = await ReadState(page, account.Uid);
        var item = state.GetProperty("data").GetProperty("inventoryItems")[0].GetProperty("id").GetString()!;
        await page.GetByLabel("Inventory item", new() { Exact = true }).SelectOptionAsync(item);
        await page.GetByLabel("Unit", new() { Exact = true }).SelectOptionAsync("kg");
        await page.GetByLabel("Quantity received", new() { Exact = true }).FillAsync("2");
        await page.GetByLabel("Total purchase cost (₱)", new() { Exact = true }).FillAsync("100");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save stock receipt", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Inventory", Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/products/new");
        await page.GetByLabel("Product name", new() { Exact = true }).FillAsync("Bread");
        await page.GetByLabel("Selling price (₱)", new() { Exact = true }).FillAsync("25");
        await page.GetByLabel("Selling price (₱)", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.GetByLabel("Selling price (₱)", new() { Exact = true })).ToHaveValueAsync("25.00");
        await page.GetByRole(AriaRole.Radio, new() { Name = "Prepared in advance", Exact = false }).ClickAsync();
        await page.GetByLabel("How many finished items does this batch normally make?", new() { Exact = true }).FillAsync("10");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add ingredient", Exact = true }).ClickAsync();
        await page.GetByLabel("Ingredient", new() { Exact = true }).SelectOptionAsync(item);
        await page.GetByLabel("Batch ingredient quantity", new() { Exact = true }).FillAsync("1000");
        await page.GetByLabel("Batch ingredient quantity", new() { Exact = true }).PressAsync("Tab");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Add ingredient", Exact = true })).ToBeDisabledAsync();
        await CheckResponsiveForm(page, host.Root, "product-recipe");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create inventory item", Exact = true }).ClickAsync();
        await page.GetByLabel("Ingredient name", new() { Exact = true }).FillAsync("Sugar");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create and add", Exact = true }).ClickAsync();
        await Expect(page.GetByLabel("Ingredient", new() { Exact = true })).ToHaveCountAsync(2);
        await Expect(page.GetByLabel("Product name", new() { Exact = true })).ToHaveValueAsync("Bread");
        await Expect(page.GetByLabel("Selling price (₱)", new() { Exact = true })).ToHaveValueAsync("25.00");
        state = await ReadState(page, account.Uid);
        Assert.Contains(state.GetProperty("data").GetProperty("inventoryItems").EnumerateArray(), x => x.GetProperty("name").GetString() == "Sugar");
        await page.GetByRole(AriaRole.Button, new() { Name = "Remove ingredient", Exact = true }).Last.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save product", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Products", Exact = true })).ToBeVisibleAsync();
        state = await ReadState(page, account.Uid);
        var bread = state.GetProperty("data").GetProperty("products").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Bread");
        Assert.Equal(10, bread.GetProperty("recipeBatchYield").GetDecimal());
        Assert.Equal(100, bread.GetProperty("recipe")[0].GetProperty("quantity").GetDecimal());
        await page.GotoAsync(host.Url + "/production/new");
        await page.GetByRole(AriaRole.Radio).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Continue to batch details", Exact = true }).ClickAsync();
        await page.GetByLabel("Planned yield", new() { Exact = true }).FillAsync("5");
        await CheckResponsiveForm(page, host.Root, "batch-requirements");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save draft", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Production", Exact = true })).ToBeVisibleAsync();
        state = await ReadState(page, account.Uid);
        var batch = state.GetProperty("data").GetProperty("batches")[0].GetProperty("id").GetString()!;
        await page.GotoAsync(host.Url + "/production/" + batch + "/complete");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "What was consumed?" })).ToBeVisibleAsync();
        await CheckResponsiveForm(page, host.Root, "batch-actuals");
        await page.GetByRole(AriaRole.Button, new() { Name = "Complete batch", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm & post", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Production", Exact = true })).ToBeVisibleAsync();
        state = await ReadState(page, account.Uid);
        Assert.Equal("completed", state.GetProperty("data").GetProperty("batches")[0].GetProperty("status").GetString());
        Assert.Equal(1500, state.GetProperty("data").GetProperty("inventoryItems")[0].GetProperty("currentQuantity").GetDecimal());
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
            await page.Locator(name == "product-recipe" ? ".recipe-list" : name == "batch-actuals" ? ".actual-list" : ".requirement-list").ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(root, "artifacts", "browser", $"{name}-{width}.png") });
        }
    }

    private static async Task<JsonElement> ReadState(IPage page, Guid user)
    {
        var json = await page.EvaluateAsync<string>("uid => mashalStorage.read('accounts',uid)", user.ToString());
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    private static Task MockApi(IBrowserContext context, AppUser account, AppData data) => context.RouteAsync("**/api/**", async route =>
    {
        var path = new Uri(route.Request.Url).AbsolutePath;
        object response = path switch
        {
            "/api/auth/session" => account,
            "/api/bootstrap" => new BootstrapResult(account, data, 0),
            "/api/auth/antiforgery" => new { token = "fixture" },
            "/api/sync/pull" => new PullResult(0, [], false),
            "/api/sync/push" => new { code = "retry", title = "Test connection interrupted" },
            "/api/reports/summary" => new FinancialSummary(7000, 3260, 1000, 3740, 2740, 1, 2),
            _ => Array.Empty<object>()
        };
        var headers = new Dictionary<string, string> { { "Access-Control-Allow-Origin", route.Request.Headers.GetValueOrDefault("origin", "*") }, { "Access-Control-Allow-Credentials", "true" }, { "Access-Control-Allow-Headers", "Content-Type,X-CSRF-TOKEN" }, { "Access-Control-Allow-Methods", "GET,POST,OPTIONS" } };
        await route.FulfillAsync(new() { Status = path == "/api/sync/push" && route.Request.Method != "OPTIONS" ? 503 : 200, ContentType = "application/json", Body = JsonSerializer.Serialize(response, Wire.Json), Headers = headers });
    });

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
