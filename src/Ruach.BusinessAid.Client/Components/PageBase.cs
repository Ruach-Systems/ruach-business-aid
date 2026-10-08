using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Client.Services;
namespace Ruach.BusinessAid.Client.Components;

public abstract class PageBase : ComponentBase, IDisposable
{
    [Inject] protected BusinessState Store { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    protected bool Saving;
    protected string Error = "";
    protected override void OnInitialized() => Store.Changed += Changed;
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    protected async Task Run(Func<Task> action, string? destination = null)
    {
        if (Saving) return;
        Saving = true; Error = "";
        try { await action(); if (destination is not null) Navigation.NavigateTo(destination); }
        catch (Exception error) { Error = error.Message; }
        finally { Saving = false; }
    }
    public virtual void Dispose() => Store.Changed -= Changed;
}
