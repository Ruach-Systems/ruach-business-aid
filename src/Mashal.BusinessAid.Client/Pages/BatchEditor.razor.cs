using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;

namespace Mashal.BusinessAid.Client.Pages;

public partial class BatchEditor
{
    [Parameter] public Guid? Id { get; set; }
    private Guid productId; private decimal yield = 1; private string note = ""; private string? version; private bool selecting = true; private ProductionBatch? existing; private bool initialized;
    private Product? Product => Store.Data.Products.Find(x => x.Id == productId);
    protected override void OnParametersSet() { if (initialized) return; initialized = true; existing = Store.Data.Batches.Find(x => x.Id == Id); if (existing is not null) { productId = existing.ProductId; yield = existing.PlannedYield; note = existing.Note ?? ""; version = existing.Version is { } v ? Convert.ToBase64String(v) : null; selecting = false; } }
    private Task Save() => Run(() => Store.Execute("saveBatch", Id ?? Guid.NewGuid(), new BatchInput(productId, yield, note), version), "/production");

}
