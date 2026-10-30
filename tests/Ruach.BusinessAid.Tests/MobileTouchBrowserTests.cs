using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task MobileTouchTargetsRemainUsableInBothDensities()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { HasTouch = true });
        var owner = new AppUser(Guid.NewGuid(), "Touch tester", "touch@example.invalid", null) { PhoneNumber = "+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Touch shop", DefaultLocation = "Main" };
        var item = new Item { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Long product name for mobile wrapping", IsActive = true, SellingPriceCentavos = 3500, UnitCostCentavos = 1000 };
        await MockApi(context, owner, new() { Business = business, Items = [item] });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Touch shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview", Exact = true })).ToBeVisibleAsync();
        foreach (var compact in new[] { false, true })
        {
            await page.EvaluateAsync("value => window.mashalUi.setCompact(value)", compact);
            foreach (var width in new[] { 320, 390, 470, 768 })
            {
                await page.SetViewportSizeAsync(width, 844);
                foreach (var route in new[] { "/dashboard", "/sales", "/items/new", "/expenses", "/stock/add", "/stock/adjust", "/sales/history", "/reports", "/settings" })
                {
                    await page.GotoAsync(host.Url + route);
                    await Expect(page.Locator(".page h1").First).ToBeVisibleAsync();
                    var failures = await page.EvaluateAsync<string[]>("""
                        () => {
                          const failures = [];
                          const visible = e => e.getClientRects().length && getComputedStyle(e).visibility !== 'hidden';
                          for (const e of document.querySelectorAll('button, .button, .sync-pill, .page-back-link, .section-heading a, .workspace-context a, .bottom-nav a, summary, input:not([type=checkbox]):not([type=radio]):not([type=hidden]), select, textarea')) {
                            if (!visible(e)) continue;
                            const r = e.getBoundingClientRect(), s = getComputedStyle(e);
                            const name = e.id || e.textContent.trim().slice(0, 40) || e.tagName;
                            if (r.height < 44 || r.width < 44) failures.push(`${name}: ${r.width} x ${r.height}`);
                            if (e.matches('input, select, textarea') && parseFloat(s.fontSize) < 16) failures.push(`${name}: font ${s.fontSize}`);
                            if (e.matches('.button.primary') && r.height < 48) failures.push(`${name}: primary height ${r.height}`);
                            if (r.left < -1 || r.right > innerWidth + 1) failures.push(`${name}: outside viewport`);
                          }
                          if (document.documentElement.scrollWidth > innerWidth + 1) failures.push('Page overflow');
                          return failures;
                        }
                        """);
                    if (route == "/sales" && width <= 390)
                        await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"touch-{width}-{compact}.png"), FullPage = true });
                    Assert.True(failures.Length == 0, $"{route}, {width}px, compact={compact}: {string.Join("; ", failures)}");
                }
            }
        }
    }
}
