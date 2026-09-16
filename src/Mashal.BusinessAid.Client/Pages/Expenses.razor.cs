using Microsoft.JSInterop;
using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class Expenses
{
    private Guid? id; private string description = "", category = "Transportation"; private string? version; private decimal amount; private DateTime date = Today().ToDateTime(TimeOnly.MinValue);
    private readonly string[] categories = ["Transportation", "Electricity", "Water", "Rent", "Packaging", "Supplies", "Labor", "Other"];
    private void Reset() { id = null; description = ""; amount = 0; version = null; }
    private void Edit(Expense value) { id = value.Id; description = value.Description; category = value.Category; amount = value.AmountCentavos / 100m; date = DateTime.Parse(value.ExpenseDate); version = value.Version is { } v ? Convert.ToBase64String(v) : null; }
    private Task Save() => Run(async () => { var value = new Expense { Id = id ?? Guid.NewGuid(), Description = description, Category = category, AmountCentavos = Centavos(amount), ExpenseDate = date.ToString("yyyy-MM-dd") }; await Store.Execute("saveExpense", value.Id, value, version); Reset(); });
    private async Task Delete(Expense value) { if (await JS.InvokeAsync<bool>("confirm", "Delete this expense?")) await Run(() => Store.Execute("deleteExpense", value.Id, new { }, value.Version is { } v ? Convert.ToBase64String(v) : null)); }

}
