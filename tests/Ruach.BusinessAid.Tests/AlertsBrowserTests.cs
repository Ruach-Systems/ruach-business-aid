using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;
public partial class BrowserTests
{
    [BrowserFact]
    public async Task BrandedAlertsHaveContrastKeyboardSafetyAndLocalAssets()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 668 }, ReducedMotion = ReducedMotion.Reduce });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.WaitForFunctionAsync("() => !!window.businessAlerts && !document.querySelector('.app-loading')");
        await page.EvaluateAsync("() => { window.alertResult = null; businessAlerts.confirm('<img src=x onerror=alert(1)>').then(result => window.alertResult = result); }");
        var dialog = page.GetByRole(AriaRole.Dialog);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.Locator("img")).ToHaveCountAsync(0);
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Shift+Tab");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true })).ToBeFocusedAsync();
        var ratios = await page.EvaluateAsync<double[]>("""
            () => {
                const lum = color => { const rgb = color.match(/[\d.]+/g).slice(0,3).map(Number).map(v => { v/=255; return v <= .04045 ? v/12.92 : ((v+.055)/1.055)**2.4; }); return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2]; };
                return [...document.querySelectorAll('.cool-alert-modal-btn,.cool-alert-modal-message,.cool-alert-modal-title')].map(el => {
                    const style = getComputedStyle(el); const bg = style.backgroundColor === 'rgba(0, 0, 0, 0)' ? 'rgb(255,255,255)' : style.backgroundColor;
                    const a=lum(style.color), b=lum(bg); return (Math.max(a,b)+.05)/(Math.min(a,b)+.05);
                });
            }
            """);
        Assert.All(ratios, ratio => Assert.True(ratio >= 4.5, $"Contrast {ratio:F2}:1"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", "branded-confirm-mobile.png") });
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => window.alertResult === false");
        Assert.False(await page.Locator("#app").EvaluateAsync<bool>("el => el.inert"));
        await page.EvaluateAsync("() => businessAlerts.success('Saved successfully.')");
        await Expect(page.Locator(".cool-alert-toast-title")).ToHaveTextAsync("Saved successfully.");
        await Expect(page.Locator(".cool-alert-toast")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("show"));
        await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", "branded-toast-mobile.png") });
        Assert.True(await page.Locator("script[src*='coolalert']").EvaluateAsync<bool>("el => new URL(el.src).origin === location.origin"));
    }

    [BrowserFact]
    public async Task MobileToastsDismissWithHorizontalSwipesButKeepShortAndVerticalGestures()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 668 }, HasTouch = true, ReducedMotion = ReducedMotion.Reduce });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.WaitForFunctionAsync("() => !!window.businessAlerts && !document.querySelector('.app-loading')");
        var touch = await context.NewCDPSessionAsync(page);
        foreach (var direction in new[] { -1, 1 })
        {
            await page.EvaluateAsync("() => businessAlerts.success('Swipe test')");
            var toast = page.Locator(".cool-alert-toast");
            await Expect(toast).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("show"));
            await Swipe(15, 0);
            await Expect(toast).ToHaveCountAsync(1);
            await Swipe(0, 70);
            await Expect(toast).ToHaveCountAsync(1);
            await Swipe(140 * direction, 0);
            await Expect(toast).ToHaveCountAsync(0);
        }
        async Task Swipe(int dx, int dy)
        {
            var box = (await page.Locator(".cool-alert-toast").BoundingBoxAsync())!;
            var x = box.X + box.Width / 2; var y = box.Y + box.Height / 2;
            await touch.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchStart", ["touchPoints"] = new[] { new { x, y } } });
            for (var step = 1; step <= 4; step++)
                await touch.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchMove", ["touchPoints"] = new[] { new { x = x + dx * step / 4, y = y + dy * step / 4 } } });
            await touch.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchEnd", ["touchPoints"] = System.Array.Empty<object>() });
        }
    }
}

