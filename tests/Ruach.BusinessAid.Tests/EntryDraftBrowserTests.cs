using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;
public partial class BrowserTests
{
 [BrowserFact]
 public async Task EntryDraftsSurviveNavigationAndStockPreviewMatchesCount()
 {
  await using var host = await Preview.Start();
  using var pw = await Playwright.CreateAsync();
  await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
  await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
  var owner = new AppUser(Guid.NewGuid(), "Draft tester", "draft@example.invalid", null) { PhoneNumber = "+639171234567" };
  var business = new Business { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Draft shop", DefaultLocation = "Main" };
  var item = new Item { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Juice", IsActive = true, CurrentQuantity = 10, SellingPriceCentavos = 3500, UnitCostCentavos = 1000 };
  await MockApi(context, owner, new() { Business = business, Items = [item] });
  var page = await context.NewPageAsync();
  await page.GotoAsync(host.Url);
  await page.GetByRole(AriaRole.Button, new() { Name = "Open Draft shop", Exact = true }).ClickAsync();
  await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Today", Exact = true })).ToBeVisibleAsync();
  await Nav("/sales");
  await page.GetByRole(AriaRole.Button, new() { Name = "Add Juice to sale", Exact = true }).ClickAsync();
  await page.GetByLabel("Juice quantity sold", new() { Exact = true }).FillAsync("2");
  await Expect(page.Locator(".sale-checkout .totals-card > div").Nth(2)).ToBeHiddenAsync();
  await page.GetByRole(AriaRole.Button, new() { Name = "Show breakdown", Exact = true }).ClickAsync();
  await Expect(page.Locator(".sale-checkout .totals-card > div").Nth(2)).ToBeVisibleAsync();
  await page.GetByRole(AriaRole.Button, new() { Name = "Hide breakdown", Exact = true }).ClickAsync();
  // A short landscape viewport leaves room for the cart controls.
  await page.SetViewportSizeAsync(740, 360);
  await Expect(page.Locator(".sale-checkout.active")).ToHaveCSSAsync("position", "static");
  await page.GetByRole(AriaRole.Button, new() { Name = "Increase Juice quantity", Exact = true }).ClickAsync();
  await Expect(page.GetByLabel("Juice quantity sold", new() { Exact = true })).ToHaveValueAsync("3");
  await page.GetByRole(AriaRole.Button, new() { Name = "Decrease Juice quantity", Exact = true }).ClickAsync();
  foreach (var compact in new[] { false, true }) {
   await page.EvaluateAsync("v => window.mashalUi.setCompact(v)", compact);
   foreach (var width in new[] { 320, 390, 470 }) {
    await page.SetViewportSizeAsync(width, 844);
    var checkout = (await page.Locator(".sale-checkout").BoundingBoxAsync())!;
    Assert.True(checkout.Height <= 160, $"Checkout too tall: {checkout.Height}");
    Assert.True(checkout.Width <= width);
    await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"checkout-{width}-{compact}.png"), FullPage = true });
   }
  }
  await page.SetViewportSizeAsync(390, 844);
  await Nav("/dashboard"); await Nav("/sales");
  await Expect(page.GetByLabel("Juice quantity sold", new() { Exact = true })).ToHaveValueAsync("2");
  await page.GetByLabel("Juice quantity sold", new() { Exact = true }).FillAsync("1000001");
  await Nav("/dashboard"); await Nav("/sales");
  await Expect(page.GetByLabel("Juice quantity sold", new() { Exact = true })).ToHaveValueAsync("1000001");
  await Expect(page.Locator(".totals-card strong").Nth(1)).ToHaveTextAsync("—");
  await Nav("/items"); await Nav("/items/new");
  await page.Locator("#item-name").FillAsync("Draft item");
  await page.Locator("#item-price").FillAsync("12.345");
  await Nav("/sales"); await Nav("/items"); await Nav("/items/new");
  await Expect(page.Locator("#item-name")).ToHaveValueAsync("Draft item");
  await Expect(page.Locator("#item-price")).ToHaveValueAsync("12.345");
  await page.GetByRole(AriaRole.Link, new() { Name = "Cancel", Exact = true }).ClickAsync();
  await Nav("/items/new");
  await Expect(page.Locator("#item-name")).ToHaveValueAsync("");
  await Nav("/items"); await Nav("/stock/adjust");
  await page.Locator("#adjust-item").SelectOptionAsync(item.Id.ToString());
  await page.Locator("#adjust-count").FillAsync("7");
  await Expect(page.GetByLabel("Stock correction preview")).ToContainTextAsync("Adjustment: -3");
  await page.Locator("#adjust-note").FillAsync("Count draft");
  await Nav("/sales"); await Nav("/items"); await Nav("/stock/adjust");
  await Expect(page.Locator("#adjust-count")).ToHaveValueAsync("7");
  await Expect(page.Locator("#adjust-note")).ToHaveValueAsync("Count draft");
  await page.Locator("#adjust-count").FillAsync("1000001");
  await Expect(page.GetByLabel("Stock correction preview")).ToContainTextAsync("Adjustment: —");
  await Nav("/items"); await Nav("/stock/add");
  await page.Locator("#receipt-item").SelectOptionAsync(item.Id.ToString());
  await page.Locator("#receipt-quantity").FillAsync("15");
  await page.Locator("#receipt-note").FillAsync("Delivery draft");
  await Nav("/sales"); await Nav("/items"); await Nav("/stock/add");
  await Expect(page.Locator("#receipt-quantity")).ToHaveValueAsync("15");
  await Expect(page.Locator("#receipt-note")).ToHaveValueAsync("Delivery draft");
  await Nav("/more"); await Nav("/expenses");
  await page.Locator("#expense-description").FillAsync("Draft fare");
  await page.Locator("#expense-amount").FillAsync("123.45");
  await Nav("/dashboard"); await Nav("/more"); await Nav("/expenses");
  await Expect(page.Locator("#expense-description")).ToHaveValueAsync("Draft fare");
  await Expect(page.Locator("#expense-amount")).ToHaveValueAsync("123.45");
  await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
  await Expect(page.Locator("#expense-description")).ToHaveValueAsync("");
  // MockApi intentionally rejects synchronization; acknowledge that unrelated error.
  await Expect(page.Locator("#cool-alert-error.active")).ToBeVisibleAsync();
  await page.Locator("#cool-alert-error").GetByRole(AriaRole.Button, new() { Name = "OK", Exact = true }).ClickAsync();
  await Nav("/dashboard"); await Nav("/more"); await Nav("/expenses");
  await Expect(page.Locator("#expense-description")).ToHaveValueAsync("");
  await Expect(page.Locator("#expense-amount")).ToHaveValueAsync("");
  async Task Nav(string href) => await page.Locator($"a[href='{href}']:visible").First.ClickAsync();
 }
}
