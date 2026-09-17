using System.Text.Json;
using Mashal.BusinessAid.Shared;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Mashal.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task SwitchingBusinessesOfflineKeepsRecordsAndQueuesSeparate()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var owner = new AppUser(Guid.NewGuid(), "Multi-business owner", "owner@example.invalid", null) { PhoneNumber = "+639181234567" };
        var a = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Cebu Kitchen", DefaultLocation = "Cebu City" } };
        var b = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Mandaue Store", DefaultLocation = "Mandaue City" } };
        await MockApi(context, owner, a, [b]);
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Your businesses", Exact = true })).ToBeVisibleAsync();
        await CheckAccountLayout(page, host.Root, "owner-businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Cebu Kitchen", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Mandaue Store", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("() => !!navigator.serviceWorker.controller");
        await context.SetOfflineAsync(true);
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Cebu Kitchen", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/expenses");
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Kitchen delivery only");
        await page.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("125");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Kitchen delivery only", new() { Exact = true })).ToBeVisibleAsync();
        var aBefore = await ReadState(page, owner.Uid);
        Assert.Single(aBefore.GetProperty("outbox").EnumerateArray());
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Mandaue Store", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/expenses");
        await Expect(page.GetByText("Kitchen delivery only", new() { Exact = true })).ToHaveCountAsync(0);
        var bState = await ReadState(page, owner.Uid);
        Assert.Empty(bState.GetProperty("data").GetProperty("expenses").EnumerateArray());
        Assert.Empty(bState.GetProperty("outbox").EnumerateArray());
        await page.ReloadAsync();
        await Expect(page.Locator(".workspace-context strong")).ToHaveTextAsync("Mandaue Store");
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Cebu Kitchen", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/expenses");
        await Expect(page.GetByText("Kitchen delivery only", new() { Exact = true })).ToBeVisibleAsync();
        var aAfter = await ReadState(page, owner.Uid);
        Assert.Equal(aBefore.GetProperty("outbox").GetRawText(), aAfter.GetProperty("outbox").GetRawText());
        Assert.Equal(a.Business.Id, aAfter.GetProperty("workspaceId").GetGuid());
        Assert.Equal(b.Business.Id, bState.GetProperty("workspaceId").GetGuid());
    }

    [BrowserFact]
    public async Task PhoneRecoveryBlocksWorkspaceAndShowsDuplicateWithoutLosingInput()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var owner = new AppUser(Guid.NewGuid(), "Recovering owner", "recovery@example.invalid", null);
        var data = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Recovery shop", DefaultLocation = "Cebu" } };
        await MockApi(context, owner, data);
        await context.RouteAsync("**/api/account/phone", async route =>
        {
            if (route.Request.Method == "POST" && route.Request.PostData!.Contains("9171234567"))
                await route.FulfillAsync(new() { Status = 409, ContentType = "application/json", Headers = ApiHeaders(route), Body = JsonSerializer.Serialize(new { code = "phone_in_use", title = "This mobile number is already linked to another account. Use another number or contact Mashal Admin." }) });
            else await route.FallbackAsync();
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url + "/dashboard");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Open Recovery shop" })).ToBeDisabledAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Update mobile number" }).ClickAsync();
        await page.GetByLabel("Philippine mobile number", new() { Exact = true }).FillAsync("09171234567");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save mobile number" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("already linked");
        await Expect(page.GetByLabel("Philippine mobile number", new() { Exact = true })).ToHaveValueAsync("09171234567");
        await CheckAccountLayout(page, host.Root, "phone-recovery");
        await page.GetByLabel("Philippine mobile number", new() { Exact = true }).FillAsync("09181234567");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save mobile number" }).ClickAsync();
        await Expect(page.GetByText("Mobile number saved.", new() { Exact = true })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/businesses");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Recovery shop" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
    }

    [BrowserFact]
    public async Task AdminReviewsRequestsAndConfirmsAuditedPhoneTransfers()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var admin = new AppUser(Guid.NewGuid(), "Mashal Admin", "admin@example.invalid", null) { IsAdmin = true };
        var first = new AdminUser(Guid.NewGuid(), "First owner", "first@example.invalid", "+639181234567", DateTimeOffset.UtcNow, 0);
        var second = new AdminUser(Guid.NewGuid(), "Second owner", "second@example.invalid", null, DateTimeOffset.UtcNow, 0);
        var request = new BusinessRequest { Id = Guid.NewGuid(), OwnerUid = first.Uid, Name = "Review shop", DefaultLocation = "Cebu", CreatedAt = DateTimeOffset.UtcNow };
        var metadata = new AdminOverview([first, second], [], [request], []);
        PhoneTransferInput? submittedTransfer = null;
        await MockApi(context, admin, new());
        await context.RouteAsync("**/api/admin**", async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (route.Request.Method == "POST" && path.EndsWith("/decision"))
            {
                var decision = JsonSerializer.Deserialize<DecisionInput>(route.Request.PostData!, Wire.Json)!;
                request.Status = decision.Approve ? "Approved" : "Rejected"; request.Reason = decision.Reason;
                if (decision.Approve) metadata.Businesses.Add(new() { Id = request.Id, OwnerUid = request.OwnerUid, Name = request.Name, DefaultLocation = request.DefaultLocation });
            }
            if (route.Request.Method == "POST" && path.EndsWith("/phone-transfer"))
            {
                submittedTransfer = JsonSerializer.Deserialize<PhoneTransferInput>(route.Request.PostData!, Wire.Json)!;
                metadata.Audit.Add(new() { Id = Guid.NewGuid(), ActorUid = admin.Uid, SubjectUid = first.Uid, RecipientUid = second.Uid, PhoneNumber = first.PhoneNumber, Action = "PhoneTransfer", Reason = submittedTransfer.Reason });
            }
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Headers = ApiHeaders(route), Body = JsonSerializer.Serialize(metadata, Wire.Json) });
        });
        var page = await context.NewPageAsync(); await page.GotoAsync(host.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Mashal Admin", Exact = true })).ToBeVisibleAsync();
        await CheckAccountLayout(page, host.Root, "admin-requests");
        await page.GetByRole(AriaRole.Button, new() { Name = "Review request", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Reject with reason", Exact = true })).ToBeDisabledAsync();
        await page.GetByLabel("Reason for rejection", new() { Exact = true }).FillAsync("Please clarify the location.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Reject with reason", Exact = true }).ClickAsync();
        Assert.Equal("Pending", request.Status);
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm rejection", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Request rejected. The owner can update and resubmit.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal("Rejected", request.Status); Assert.Empty(metadata.Businesses);
        // A subsequent submission is reviewed independently.
        request = new() { Id = Guid.NewGuid(), OwnerUid = first.Uid, Name = "Resubmitted shop", DefaultLocation = "Cebu City", CreatedAt = DateTimeOffset.UtcNow };
        metadata.Requests.Add(request);
        await page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Review request", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve business", Exact = true }).ClickAsync();
        Assert.Empty(metadata.Businesses);
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm approval", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Business approved. Its empty workspace is ready.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Single(metadata.Businesses);
        await page.GetByRole(AriaRole.Button, new() { Name = "Users", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Transfer phone number", Exact = true }).First.ClickAsync();
        await page.GetByLabel("Recipient account", new() { Exact = true }).SelectOptionAsync(second.Uid.ToString());
        await page.GetByLabel("Reason / identity review reference", new() { Exact = true }).FillAsync("Offline identity review completed: case 123.");
        await page.GetByRole(AriaRole.Checkbox).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Review transfer", Exact = true }).ClickAsync();
        Assert.Null(submittedTransfer);
        await CheckAccountLayout(page, host.Root, "admin-transfer");
        await page.GetByRole(AriaRole.Button, new() { Name = "Confirm transfer", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "PhoneTransfer", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(first.Uid, submittedTransfer!.FromUserId); Assert.Equal(second.Uid, submittedTransfer.ToUserId);
        Assert.True(submittedTransfer.IdentityChecked); Assert.Null(submittedTransfer.ExpectedRecipientPhone);
    }

    private static async Task CheckAccountLayout(IPage page, string root, string name)
    {
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "browser"));
        foreach (var width in new[] { 320, 390, 768, 1366 })
        {
            await page.SetViewportSizeAsync(width, 900);
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1 && [...document.querySelectorAll('.account-shell,main')].every(e => e.scrollWidth <= e.clientWidth + 1)"), $"Overflow in {name} at {width}px");
            await page.ScreenshotAsync(new() { Path = Path.Combine(root, "artifacts", "browser", $"{name}-{width}.png"), FullPage = true });
        }
    }
    private static Dictionary<string, string> ApiHeaders(IRoute route) => new()
    {
        ["Access-Control-Allow-Origin"] = route.Request.Headers.GetValueOrDefault("origin", "*"),
        ["Access-Control-Allow-Credentials"] = "true", ["Access-Control-Allow-Headers"] = "Content-Type,X-CSRF-TOKEN",
        ["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS"
    };
}
