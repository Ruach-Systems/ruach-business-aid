using Microsoft.JSInterop;
using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class Expenses
{
    private Guid? id; private string description = "", category = "Transportation"; private string? version; private decimal amount; private DateTime date = Today().ToDateTime(TimeOnly.MinValue);
    private bool amountValid = true;
    private int amountRevision;
    private Microsoft.AspNetCore.Components.ElementReference expenseForm;
    private bool revealEditor; private bool clearFieldErrors;
    private string editingSummary = "";
    private string successMessage = "";
    private bool revealSuccess;
    private CancellationTokenSource? successDismissal;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (clearFieldErrors) { clearFieldErrors = false; await JS.InvokeVoidAsync("mashalUi.clearFieldErrors", expenseForm); }
        if (revealSuccess)
        {
            revealSuccess = false;
            await JS.InvokeVoidAsync("businessAlerts.success", successMessage);
        }
        if (!revealEditor) return;
        revealEditor = false;
        await JS.InvokeVoidAsync("mashalUi.revealExpenseEditor", expenseForm);
    }
    private readonly string[] categories = ["Transportation", "Electricity", "Water", "Rent", "Packaging", "Supplies", "Labor", "Other"];
    private void Reset() { clearFieldErrors = true; ClearSuccess(); Error = ""; date = Today().ToDateTime(TimeOnly.MinValue); category = categories[0]; editingSummary = ""; amountRevision++; amountValid = true; id = null; description = ""; amount = 0; version = null; }
    private void Edit(Expense value) { clearFieldErrors = true; ClearSuccess(); Error = ""; amountRevision++; amountValid = true; id = value.Id; description = value.Description; category = value.Category; amount = value.AmountCentavos / 100m; date = DateTime.Parse(value.ExpenseDate); version = value.Version is { } v ? Convert.ToBase64String(v) : null; editingSummary = $"{value.Description} · {Money(value.AmountCentavos)} · {value.ExpenseDate}"; revealEditor = true; }
    private Task Save() => Run(async () => { ClearSuccess(); var updating = id is not null; Rules.Require(amountValid, "Correct the expense amount before saving."); var value = new Expense { Id = id ?? Guid.NewGuid(), Description = description, Category = category, AmountCentavos = Centavos(amount), ExpenseDate = date.ToString("yyyy-MM-dd") }; await Store.Execute("saveExpense", value.Id, value, version); Reset(); successMessage = $"Expense {(updating ? "updated" : "saved")} successfully."; revealSuccess = true; successDismissal = new CancellationTokenSource(); _ = DismissSuccessAsync(successDismissal.Token); });
    private async Task Delete(Expense value) { if (await JS.InvokeAsync<bool>("businessAlerts.confirm",  $"Delete {value.Description} ({Money(value.AmountCentavos)})?", "Delete expense")) await Run(() => Store.Execute("deleteExpense", value.Id, new { }, value.Version is { } v ? Convert.ToBase64String(v) : null), success: "Expense deleted successfully."); }

    private void ClearSuccess()
    {
        successDismissal?.Cancel();
        successDismissal?.Dispose();
        successDismissal = null;
        successMessage = "";
        revealSuccess = false;
    }

    private async Task DismissSuccessAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            await InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                successMessage = "";
                StateHasChanged();
            });
        }
        catch (OperationCanceledException) { }
    }

    public override void Dispose()
    {
        ClearSuccess();
        base.Dispose();
    }
}
