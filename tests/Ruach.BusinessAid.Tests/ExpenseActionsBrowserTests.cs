using Microsoft.Playwright;
using Ruach.BusinessAid.Shared;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace Ruach.BusinessAid.Tests;

public partial class BrowserTests
{
    [BrowserFact]
    public async Task ExpenseActionsStayInsideCardsAndPreserveEditAndDeleteConfirmation()
    {
        await using var host = await Preview.Start();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync();
        var owner = new AppUser(Guid.NewGuid(), "Expense tester", "expenses@example.invalid", null) { PhoneNumber = "+639171234567" };
        var expense = new Expense { Id = Guid.NewGuid(), Description = "Transportation and packaging supplies", Category = "Transportation", AmountCentavos = 5000, ExpenseDate = "2026-09-15", Version = [0, 0, 0, 0, 0, 0, 0, 1] };
        var data = new AppData { Business = new() { Id = Guid.NewGuid(), OwnerUid = owner.Uid, Name = "Expense shop", DefaultLocation = "Main" }, Expenses = [expense] };
        expense.BusinessId = data.Business.Id;
        await MockApi(context, owner, data);
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.Url);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Expense shop", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Business overview" })).ToBeVisibleAsync();
        await page.GotoAsync(host.Url + "/expenses");
        var card = page.Locator("article.expense-entry");
        var edit = card.GetByRole(AriaRole.Button, new() { Name = $"Edit {expense.Description} expense", Exact = true });
        var delete = card.GetByRole(AriaRole.Button, new() { Name = $"Delete {expense.Description} expense", Exact = true });
        foreach (var compact in new[] { false, true })
        {
            await page.EvaluateAsync("compact => window.mashalUi.setCompact(compact)", compact);
            foreach (var width in new[] { 320, 390, 1366 })
            {
                await page.SetViewportSizeAsync(width, 900);
                await card.ScrollIntoViewIfNeededAsync();
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
                var bounds = (await card.BoundingBoxAsync())!;
                foreach (var button in new[] { edit, delete })
                {
                    var box = (await button.BoundingBoxAsync())!;
                    Assert.True(box.X >= bounds.X && box.X + box.Width <= bounds.X + bounds.Width + 1);
                    Assert.True(box.Height >= 44 && box.Y >= bounds.Y && box.Y + box.Height <= bounds.Y + bounds.Height + 1);
                }
                await page.ScreenshotAsync(new() { Path = Path.Combine(host.Root, "artifacts", "browser", $"expense-actions-{width}-{(compact ? "compact" : "standard")}.png"), FullPage = true });
            }
        }
        await edit.ClickAsync();
        await Expect(page.GetByLabel("Description", new() { Exact = true })).ToHaveValueAsync(expense.Description);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Update expense", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel edit", Exact = true }).ClickAsync();
        var confirmed = false;
        page.Dialog += async (_, dialog) => { Assert.Equal("Delete this expense?", dialog.Message); if (confirmed) await dialog.AcceptAsync(); else await dialog.DismissAsync(); };
        await delete.ClickAsync();
        await Expect(card).ToHaveCountAsync(1);
        confirmed = true;
        await delete.ClickAsync();
        await Expect(card).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No expenses recorded", Exact = true })).ToBeVisibleAsync();
    }
}
