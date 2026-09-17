using Microsoft.AspNetCore.Components;
using Mashal.BusinessAid.Shared;
using static Mashal.BusinessAid.Shared.Calculations;

namespace Mashal.BusinessAid.Client.Pages;

public partial class ProductEditor
{
    [Parameter] public Guid? Id { get; set; }
    private Product draft = new(); private decimal price, manualCost; private string? version; private Guid? loaded;
    private List<BatchRecipeLine> batchRecipe = [];
    private string quickItemName = "", quickItemUnit = "g";
    private bool showQuickItem;
    protected override void OnParametersSet()
    {
        if (draft.Id != Guid.Empty && loaded == Id) return;
        loaded = Id;
        draft = LocalData.Clone(Store.Data.Products.Find(x => x.Id == Id) ?? new Product { Id = Guid.NewGuid(), InventoryMode = "untracked", IsActive = true });
        version = draft.Version is { } v ? Convert.ToBase64String(v) : null;
        price = draft.SellingPriceCentavos / 100m;
        manualCost = draft.ManualCostCentavos / 100m;
        draft.RecipeBatchYield = draft.RecipeBatchYield > 0 ? draft.RecipeBatchYield : 1;
        batchRecipe = draft.Recipe.Select(x => new BatchRecipeLine(x.InventoryItemId, BatchQuantity(x.Quantity, draft.RecipeBatchYield))).ToList();
    }
    private IEnumerable<RecipeLine> NormalizedRecipe => batchRecipe.Select(x => new RecipeLine { InventoryItemId = x.InventoryItemId, Quantity = PerItemQuantity(x.BatchQuantity, draft.RecipeBatchYield) });
    private void SetBatchQuantity(BatchRecipeLine line, string? value)
    {
        if (decimal.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var quantity)) line.BatchQuantity = quantity;
    }
    private string ProductError => !Store.Inventory.Any() && Error == "Add at least one batch ingredient."
        ? "Create your first inventory ingredient in the Batch ingredients section."
        : Error;
    private long Cost
    {
        get
        {
            if (draft.InventoryMode == "untracked") return Centavos(manualCost);
            var costing = LocalData.Clone(draft); costing.Recipe = NormalizedRecipe.ToList();
            return ProductCost(costing, Store.Inventory);
        }
    }
    private IEnumerable<InventoryItem> AvailableIngredients => Store.Inventory.Where(x => x.Id != draft.FinishedInventoryItemId && !batchRecipe.Any(line => line.InventoryItemId == x.Id));
    private void AddLine()
    {
        if (AvailableIngredients.FirstOrDefault() is { } item) batchRecipe.Add(new(item.Id, 1));
    }
    private Task CreateFirstIngredient() => Run(async () =>
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            Name = Rules.Name(quickItemName),
            BaseUnit = quickItemUnit,
            UnitKind = quickItemUnit == "g" ? "mass" : quickItemUnit == "ml" ? "volume" : quickItemUnit == "pc" ? "count" : "custom",
            IsActive = true
        };
        await Store.Execute("saveInventoryItem", item.Id, item);
        batchRecipe.Add(new(item.Id, 1));
        quickItemName = "";
        showQuickItem = false;
    });
    private Task Save() => Run(async () =>
    {
        Rules.Require(price > 0, "Enter a selling price.");
        if (draft.InventoryMode == "prepared")
        {
            Rules.Require(draft.RecipeBatchYield > 0, "Enter how many finished items the reference batch makes.");
            Rules.Require(Store.Inventory.Any(), "Create your first inventory ingredient in the Batch ingredients section.");
            Rules.Require(batchRecipe.Count > 0, "Add at least one batch ingredient.");
            Rules.Require(batchRecipe.All(x => x.InventoryItemId != Guid.Empty && x.BatchQuantity > 0), "Select every ingredient and enter a positive batch quantity.");
            draft.Recipe = NormalizedRecipe.ToList();
        }
        draft.SellingPriceCentavos = Centavos(price);
        draft.ManualCostCentavos = Centavos(manualCost);
        await Store.SaveProduct(draft, version);
    }, "/products");

    private sealed class BatchRecipeLine(Guid inventoryItemId, decimal batchQuantity)
    {
        public Guid InventoryItemId { get; set; } = inventoryItemId;
        public decimal BatchQuantity { get; set; } = batchQuantity;
    }

}
