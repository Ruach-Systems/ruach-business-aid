using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class BatchComplete
{
    private void SetActualQuantity(Guid itemId, string? value)
    {
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var quantity)) actual[itemId] = quantity;
    }
    [Parameter] public Guid Id { get; set; }
    private ProductionBatch? batch; private Dictionary<Guid, decimal> actual = []; private decimal yield; private string note = ""; private string? version; private bool confirming;
    protected override void OnParametersSet() { if (batch is not null) return; batch = LocalData.Clone(Store.Data.Batches.Find(x => x.Id == Id)); if (batch is not null) { actual = batch.Ingredients.ToDictionary(x => x.InventoryItemId, x => x.ActualQuantity); yield = batch.PlannedYield; note = batch.Note ?? ""; version = batch.Version is { } v ? Convert.ToBase64String(v) : null; } }
    private long Total => batch?.Ingredients.Sum(x => Rules.Round(actual.GetValueOrDefault(x.InventoryItemId) * (Store.Inventory.FirstOrDefault(i => i.Id == x.InventoryItemId)?.AverageCostCentavos ?? x.UnitCostCentavos))) ?? 0;
    private Task Complete() => Run(() => Store.Execute("completeBatch", Id, new CompleteInput(yield, actual, note), version), "/production");

}
