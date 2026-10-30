using Microsoft.AspNetCore.Components;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Client.Pages;

public partial class StockAdjustment
{
    private sealed record FormDraft(Guid Item, long Count, string Reason, string Note, string? Version);
    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (RestoreDraft<FormDraft>() is { } d) { itemId = d.Item; count = d.Count; reason = d.Reason; note = d.Note; version = d.Version; restored = true; }
    }
    private bool restored;

    [SupplyParameterFromQuery(Name = "item")] public Guid? SelectedItem { get; set; }
    private Guid itemId;
    private long count;
    private bool countValid = true;
    private string reason = "Stock count correction", note = "";
    private string? version;
    protected override void OnParametersSet() { if (restored) return; if (SelectedItem is { } id) { itemId = id; Choose(); } }
    private void Choose()
    {
        NumberDrafts.Remove("adjust-count");
        var item = Store.AllItems.FirstOrDefault(x => x.Id == itemId);
        count = Math.Max(0, item?.CurrentQuantity ?? 0);
        version = item?.Version is { } value ? Convert.ToBase64String(value) : null;
    }
    private Task Save() => Run(async () => { Rules.Require(countValid, "Correct the counted quantity before saving."); await Store.Execute("adjustStock", Guid.NewGuid(), new AdjustmentInput(itemId, count, reason, note), version); ClearDraft(); }, "/items", "Stock corrected successfully.");
    public override void Dispose() { KeepDraft(new FormDraft(itemId, count, reason, note, version)); base.Dispose(); }
}
