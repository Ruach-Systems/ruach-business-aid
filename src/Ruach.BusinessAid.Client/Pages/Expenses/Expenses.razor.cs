using Microsoft.JSInterop;
using Ruach.BusinessAid.Shared;
using static Ruach.BusinessAid.Shared.Calculations;

namespace Ruach.BusinessAid.Client.Pages;

public partial class Expenses
{
    private sealed record FormDraft(Guid? Id, string Description, string Category, string? Version, decimal Amount, DateTime Date, string Summary);
    protected override void OnInitialized()
    {
        base.OnInitialized(); SetPeriod();
        if (RestoreDraft<FormDraft>() is { } d) { id = d.Id; description = d.Description; category = d.Category; version = d.Version; amount = d.Amount; date = d.Date; editingSummary = d.Summary; }
    }

    private string period = "month", filterCategory = "", search = "";
    private DateTime? from, to;
    private int page = 1;
    private void ResetPage() => page = 1;
    private void ResetFilters() { period = "month"; filterCategory = search = ""; SetPeriod(); }
    private void SetPeriod() {
        page = 1;
        var today = Today().ToDateTime(TimeOnly.MinValue);
        to = period == "yesterday" ? today.AddDays(-1) : today;
        from = period switch {
            "all" => null, "today" => today, "yesterday" => to,
            "week" => today.AddDays(-(((int)today.DayOfWeek+6)%7)),
            _ => new DateTime(today.Year,today.Month,1)
        };
        if(period == "all") to = null;
    }
    private bool InvalidRange => period == "custom" && (from is null || to is null || from > to || from < new DateTime(2026,1,1) || to > Today().ToDateTime(TimeOnly.MinValue));
    private List<Expense> Filtered => InvalidRange ? [] : Store.Expenses
        .Where(x => (from is null || string.CompareOrdinal(x.ExpenseDate,from.Value.ToString("yyyy-MM-dd"))>=0)
            && (to is null || string.CompareOrdinal(x.ExpenseDate,to.Value.ToString("yyyy-MM-dd"))<=0)
            && (filterCategory == "" || x.Category == filterCategory)
            && x.Description.Contains(search.Trim(),StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x=>x.ExpenseDate).ThenByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).ToList();
    private int PageCount => Math.Max(1,(Filtered.Count+9)/10);
    private int CurrentPage => Math.Min(page,PageCount);
    private string RangeLabel => period == "all" ? "All dates" : $"{from:MMM d, yyyy} – {to:MMM d, yyyy}";
    private static string ExpenseIcon(string category) => category switch {
        "Transportation" => "fuel", "Electricity" => "bolt", "Water" => "water",
        "Rent" => "home", "Labor" => "users", "Packaging" or "Supplies" => "cube", _ => "wallet"
    };

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
    private void Reset() { ClearDraft(); clearFieldErrors = true; ClearSuccess(); Error = ""; date = Today().ToDateTime(TimeOnly.MinValue); category = categories[0]; editingSummary = ""; amountRevision++; amountValid = true; id = null; description = ""; amount = 0; version = null; }
    private void Edit(Expense value) { ClearDraft(); clearFieldErrors = true; ClearSuccess(); Error = ""; amountRevision++; amountValid = true; id = value.Id; description = value.Description; category = value.Category; amount = value.AmountCentavos / 100m; date = DateTime.Parse(value.ExpenseDate); version = value.Version is { } v ? Convert.ToBase64String(v) : null; editingSummary = $"{value.Description} · {Money(value.AmountCentavos)} · {value.ExpenseDate}"; revealEditor = true; }
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
        KeepDraft(new FormDraft(id, description, category, version, amount, date, editingSummary));
        ClearSuccess();
        base.Dispose();
    }
}
