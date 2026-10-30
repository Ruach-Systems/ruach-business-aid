using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ruach.BusinessAid.Client.Services;
namespace Ruach.BusinessAid.Client.Components;

public abstract class PageBase : ComponentBase, IDisposable
{
    [Inject] protected BusinessState Store { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    [Inject] protected IJSRuntime NotificationJS { get; set; } = default!;
    private string? draftKey;
    private bool draftCleared;
    protected Dictionary<string, string> NumberDrafts { get; private set; } = [];
    protected T? RestoreDraft<T>() where T : class
    {
        draftKey = $"{Store.User?.Uid}:{Store.Data.Business?.Id}:{Navigation.ToBaseRelativePath(Navigation.Uri)}";
        if (Store.FormDrafts.GetValueOrDefault(draftKey + ":numbers") is Dictionary<string, string> numbers) NumberDrafts = numbers;
        return Store.FormDrafts.GetValueOrDefault(draftKey) as T;
    }
    protected void KeepDraft(object draft)
    {
        if (draftKey is null || draftCleared) return;
        Store.FormDrafts[draftKey] = draft;
        Store.FormDrafts[draftKey + ":numbers"] = NumberDrafts;
    }
    protected void ClearDraft()
    {
        if (draftKey is not null) { Store.FormDrafts.Remove(draftKey); Store.FormDrafts.Remove(draftKey + ":numbers"); }
        NumberDrafts.Clear();
    }
    protected void CancelDraft() { ClearDraft(); draftCleared = true; }
    protected bool Saving;
    protected string Error = "";
    protected override void OnInitialized() => Store.Changed += Changed;
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    protected async Task Run(Func<Task> action, string? destination = null, string? success = null)
    {
        if (Saving) return;
        Saving = true; Error = "";
        try { await action(); if (destination is not null) draftCleared = true; if (success is not null) await NotificationJS.InvokeVoidAsync("businessAlerts.success", success); if (destination is not null) Navigation.NavigateTo(destination); }
        catch (Exception error) { Error = error.Message; }
        finally { Saving = false; }
    }
    public virtual void Dispose() => Store.Changed -= Changed;
}
