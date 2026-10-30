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
        await Expect(page.Locator(".numeric-error")).ToHaveCountAsync(0);
        await page.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("1e3");
        await page.GetByLabel("Category", new() { Exact = true }).FocusAsync();
        await Expect(page.Locator(".numeric-error")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save expense", Exact = true }).ClickAsync();
        await Expect(page.Locator(".numeric-error")).ToHaveCountAsync(2);
        await Expect(page.GetByText("Enter description.", new() { Exact = true })).ToBeVisibleAsync();
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
                await edit.ClickAsync();
                await Expect(page.GetByLabel("Description", new() { Exact = true })).ToBeFocusedAsync();
                await Expect(page.Locator(".edit-notice")).ToContainTextAsync(expense.Description);
                await Expect(page.Locator(".editing-label")).ToHaveTextAsync("Editing this expense");
                var notice = (await page.Locator(".edit-notice").BoundingBoxAsync())!;
                Assert.True(notice.Y >= 0 && notice.Y + notice.Height < 900);
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
                await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Unsaved edit");
                await page.GetByLabel("Category", new() { Exact = true }).SelectOptionAsync("Rent");
                await page.GetByRole(AriaRole.Button, new() { Name = "Cancel edit", Exact = true }).ClickAsync();
                await AssertDefaultExpenseForm();
                await Expect(page.Locator(".edit-notice")).ToHaveCountAsync(0);
                await Expect(page.Locator(".editing-label")).ToHaveCountAsync(0);
                await Expect(card).ToContainTextAsync(expense.Description);
            }
        }
        await edit.ClickAsync();
        await Expect(page.GetByLabel("Description", new() { Exact = true })).ToHaveValueAsync(expense.Description);
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Update expense", Exact = true })).ToBeVisibleAsync();
        await page.SetViewportSizeAsync(390, 668);
        await page.GetByLabel("Amount (₱)", new() { Exact = true }).FillAsync("75.50");
        await page.GetByLabel("Category", new() { Exact = true }).SelectOptionAsync("Rent");
        await page.GetByRole(AriaRole.Button, new() { Name = "Update expense", Exact = true }).ClickAsync();
        var success = page.Locator(".cool-alert-toast");
        await Expect(success.Locator(".cool-alert-toast-title")).ToHaveTextAsync("Expense updated successfully.");
        await AssertDefaultExpenseForm();

        await Expect(success).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("show"));
        var successBounds = (await success.BoundingBoxAsync())!;
        Assert.True(successBounds.Y >= 0 && successBounds.Y + successBounds.Height < 668);
        await Expect(card.Locator(".expense-amount")).ToHaveTextAsync("₱75.50");
        await Expect(success).ToHaveCountAsync(0, new() { Timeout = 7000 });
        if (await page.GetByRole(AriaRole.Dialog).CountAsync() > 0) await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "OK", Exact = true }).ClickAsync();
        await edit.ClickAsync();
        await Expect(success).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel edit", Exact = true }).ClickAsync();
        await delete.ClickAsync();
        await Expect(page.Locator(".cool-alert-modal")).ToBeVisibleAsync();
        await page.Locator(".cool-alert-modal").GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(card).ToHaveCountAsync(1);
        await delete.ClickAsync();
        await page.Locator(".cool-alert-modal").GetByRole(AriaRole.Button, new() { Name = "Delete expense", Exact = true }).ClickAsync();
        await Expect(card).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "No expenses recorded", Exact = true })).ToBeVisibleAsync();

        async Task AssertDefaultExpenseForm()
        {
            await Expect(page.GetByLabel("Date", new() { Exact = true })).ToHaveValueAsync(Calculations.Today().ToString("yyyy-MM-dd"));
            await Expect(page.GetByLabel("Description", new() { Exact = true })).ToHaveValueAsync("");
            await Expect(page.GetByLabel("Amount (₱)", new() { Exact = true })).ToHaveValueAsync("");
            await Expect(page.GetByLabel("Category", new() { Exact = true })).ToHaveValueAsync("Transportation");
        }
    }
}
