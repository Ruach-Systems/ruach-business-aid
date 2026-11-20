using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;
namespace Ruach.BusinessAid.Tests;
public partial class BrowserTests
{
 [BrowserFact]
 public async Task MoneyTypingKeepsCaretAndFormatsOnlyOnBlur()
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
  await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview", Exact = true })).ToBeVisibleAsync();
  await CheckFields("/expenses", new[] { "#expense-amount" });
  await CheckFields("/items/new", new[] { "#item-price", "#item-cost" });
  await CheckFields($"/items/{item.Id}/edit", new[] { "#item-price", "#item-cost" });
  await page.GotoAsync(host.Url + "/sales");
  await page.GetByRole(AriaRole.Button, new() { Name = "Add Juice to sale", Exact = true }).ClickAsync();
  var quantity = page.GetByLabel("Juice quantity sold", new() { Exact = true });
  await quantity.PressAsync("ControlOrMeta+a");
  await quantity.PressSequentiallyAsync("abc1e+2-.3");
  await Expect(quantity).ToHaveValueAsync("123");
  await quantity.PressAsync("ControlOrMeta+a");
  await quantity.PressSequentiallyAsync("4");
  await Expect(quantity).ToHaveValueAsync("4");
  async Task CheckFields(string route, string[] selectors) {
   await page.GotoAsync(host.Url + route);
   foreach (var selector in selectors) {
    var input = page.Locator(selector);
    await input.FocusAsync();
    await Expect(input).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^(?!.*\.00$).*$"));
    await input.PressAsync("ControlOrMeta+a");
    await input.PressAsync("Backspace");
    await input.FillAsync("");
    await input.PressSequentiallyAsync("abcEe+-₱ ,");
    await Expect(input).ToHaveValueAsync("");
    var expected = "";
    foreach (var character in "1234.56") {
     await input.PressSequentiallyAsync(character.ToString(), new() { Delay = 120 });
     expected += character;
     await Expect(input).ToHaveValueAsync(expected);
     Assert.Equal(expected.Length, await input.EvaluateAsync<int>("e => e.selectionStart"));
    }
    await input.FillAsync("");
    await input.PressSequentiallyAsync("123", new() { Delay = 120 });
    await Expect(input).ToHaveValueAsync("123");
    await input.PressAsync("Home");
    await input.PressAsync("ArrowRight");
    await input.PressSequentiallyAsync("x");
    Assert.Equal(1, await input.EvaluateAsync<int>("e => e.selectionStart"));
    await Expect(input).ToHaveValueAsync("123");
    await input.PressAsync("Tab");
    await Expect(input).ToHaveValueAsync("123.00");
    Assert.True(await input.EvaluateAsync<bool>("""
      e => {
        const data = new DataTransfer(); data.setData('text/plain', '1e3');
        return !e.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
      }
      """));
    await Expect(input).ToHaveValueAsync("123.00");
    await input.FocusAsync();
    await Expect(input).ToHaveValueAsync("123");
    await input.EvaluateAsync("e => { e.value = 'abc'; e.dispatchEvent(new InputEvent('input', { bubbles: true })); }");
    await Expect(input).ToHaveValueAsync("123");
    await input.FillAsync("1234.56");
    await input.PressAsync("Tab");
    await Expect(input).ToHaveValueAsync("1234.56");
    await input.FocusAsync();
    await Expect(input).ToHaveValueAsync("1234.56");
    await input.PressAsync("End");
    await input.PressSequentiallyAsync(".,");
    await Expect(input).ToHaveValueAsync("1234.56");
    // Replacing a selected decimal point with another one is still valid.
    await input.EvaluateAsync("e => e.setSelectionRange(4, 5)");
    await input.PressSequentiallyAsync(".");
    await Expect(input).ToHaveValueAsync("1234.56");
    await input.FillAsync("");
    await input.PressSequentiallyAsync("12.345", new() { Delay = 120 });
    await input.PressAsync("Tab");
    await Expect(input).ToHaveValueAsync("12.345");
   }
  }
 }
}
