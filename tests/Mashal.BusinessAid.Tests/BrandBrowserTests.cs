using System.Text.Json;
using Mashal.BusinessAid.Shared;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Mashal.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task BrandedNewOriginBootstrapsWithoutMovingOldOriginOutbox()
    {
        await using var oldHost = await Preview.Start();
        await using var newHost = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = Environment.GetEnvironmentVariable("MASHAL_BROWSER_CHANNEL") });
        await using var context = await browser.NewContextAsync();
        var account = new AppUser(Guid.NewGuid(), "Existing owner", "owner@example.invalid", null) { PhoneNumber = "+639171234567" };
        var business = new Business { Id = Guid.NewGuid(), OwnerUid = account.Uid, Name = "Existing business", DefaultLocation = "Main" };
        await MockApi(context, account, new AppData { Business = business });
        var oldPage = await context.NewPageAsync();
        await oldPage.GotoAsync(oldHost.Url);
        await oldPage.GetByRole(AriaRole.Button, new() { Name = "Open Existing business", Exact = true }).ClickAsync();
        await Expect(oldPage.GetByRole(AriaRole.Heading, new() { Name = "Business overview", Exact = true })).ToBeVisibleAsync();
        var key = $"{account.Uid}:{business.Id}";
        await oldPage.WaitForFunctionAsync("async key => (await mashalStorage.read('accounts', key)) !== null", key);
        await oldPage.EvaluateAsync("""
            async key => {
              const state = JSON.parse(await mashalStorage.read('accounts', key));
              state.outbox = [{ id: 'pending-old-device', type: 'saveExpense', payload: { description: 'Do not migrate' } }];
              await mashalStorage.write('accounts', key, JSON.stringify(state));
            }
            """, key);
        var newPage = await context.NewPageAsync();
        await newPage.GotoAsync(newHost.Url);
        await newPage.GetByRole(AriaRole.Button, new() { Name = "Open Existing business", Exact = true }).ClickAsync();
        await Expect(newPage.GetByRole(AriaRole.Heading, new() { Name = "Business overview", Exact = true })).ToBeVisibleAsync();
        await newPage.WaitForFunctionAsync("async key => (await mashalStorage.read('accounts', key)) !== null", key);
        var newState = await ReadState(newPage, account.Uid);
        Assert.Equal(business.Id, newState.GetProperty("data").GetProperty("business").GetProperty("id").GetGuid());
        Assert.Equal(0, newState.GetProperty("outbox").GetArrayLength());
        Assert.Equal(1, (await ReadState(oldPage, account.Uid)).GetProperty("outbox").GetArrayLength());
        await newPage.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");
        await newPage.EvaluateAsync("() => document.fonts.ready");
        Assert.Contains("Business Aid by RUACH", await newPage.TitleAsync());
        foreach (var width in new[] { 320, 1366 })
        {
            await newPage.SetViewportSizeAsync(width, 900);
            var logo = newPage.Locator("img[alt='Business Aid by RUACH']:visible").First;
            await Expect(logo).ToBeVisibleAsync();
            Assert.True(await logo.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0"));
            Assert.True(await newPage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1"));
            Directory.CreateDirectory(Path.Combine(newHost.Root, "artifacts", "browser"));
            await newPage.ScreenshotAsync(new() { Path = Path.Combine(newHost.Root, "artifacts", "browser", $"ruach-dashboard-{width}.png"), FullPage = true });
        }
        Assert.True(await newPage.EvaluateAsync<bool>("""
            async () => {
              const manifest = await (await fetch('/manifest.webmanifest')).json();
              const icon = manifest.icons.find(x => x.purpose === 'maskable');
              const image = new Image(); image.src = icon.src; await image.decode();
              const canvas = document.createElement('canvas'); canvas.width = canvas.height = 512;
              const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0);
              const pixels = ctx.getImageData(0, 0, 512, 512).data;
              for (let y = 0; y < 512; y++) for (let x = 0; x < 512; x++) {
                const i = (y * 512 + x) * 4;
                if (pixels[i + 3] !== 255) return false;
                if (Math.hypot(x - 256, y - 256) > 204.8 &&
                    (pixels[i] !== 178 || pixels[i + 1] !== 31 || pixels[i + 2] !== 50)) return false;
              }
              return true;
            }
            """));
        await context.SetOfflineAsync(true);
        await newPage.ReloadAsync();
        await Expect(newPage.Locator("main h1")).ToBeVisibleAsync();
        Assert.Equal(0, (await ReadState(newPage, account.Uid)).GetProperty("outbox").GetArrayLength());
        Assert.Equal(1, (await ReadState(oldPage, account.Uid)).GetProperty("outbox").GetArrayLength());
    }
}
