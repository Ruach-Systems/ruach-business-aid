using System.Text.Json;

namespace Mashal.BusinessAid.Shared;
// Each handler receives a business-scoped repository within the push transaction.
public sealed class CommandProcessor(IEntityRepository repository, bool checkConcurrency = true)
{
    private void CheckVersion(Entity? current, string? expected)
    {
        if (checkConcurrency)
            Rules.CheckVersion(current, expected);
    }

    public async Task Execute(Operation op)
    {
        Rules.Require(op.EntityId != Guid.Empty && op.Id != Guid.Empty, "Operation and entity IDs are required.");
        switch (op.Type)
        {
            case "saveInventoryItem":
                await Inventory(op);
                break;
            case "saveProduct":
                await Product(op);
                break;
            case "saveExpense":
                await Expense(op, false);
                break;
            case "deleteExpense":
                await Expense(op, true);
                break;
            case "recordSale":
                await Sale(op);
                break;
            case "receiveStock":
                await Receive(op);
                break;
            case "adjustStock":
                await Adjust(op);
                break;
            case "saveBatch":
                await Batch(op);
                break;
            case "completeBatch":
                await Complete(op);
                break;
            default:
                throw new DomainException("validation", "Unknown operation type.");
        }
    }

    private static T Input<T>(Operation op) => Wire.Read<T>(op.Payload);
    private async Task Inventory(Operation op)
    {
        var input = Input<InventoryItem>(op);
        var old = await repository.Find<InventoryItem>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        var item = old ?? new InventoryItem
        {
            Id = op.EntityId
        };
        item.Name = Rules.Name(input.Name);
        item.MinimumQuantity = input.MinimumQuantity;
        item.IsActive = input.IsActive;
        Rules.Require(input.MinimumQuantity >= 0, "Minimum quantity cannot be negative.");
        Rules.Require(new[] { "mass", "volume", "count", "custom" }.Contains(input.UnitKind), "Invalid unit kind.");
        Rules.Require(!string.IsNullOrWhiteSpace(input.BaseUnit) && input.BaseUnit.Length <= 32, "Invalid base unit.");
        if (old is not null)
            Rules.Require(item.BaseUnit == input.BaseUnit && item.UnitKind == input.UnitKind, "An existing item's base unit cannot change.");
        item.BaseUnit = input.BaseUnit;
        item.UnitKind = input.UnitKind;
        await repository.Save(item);
    }

    private async Task Product(Operation op)
    {
        var input = Input<Product>(op);
        var old = await repository.Find<Product>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        input.Id = op.EntityId;
        input.CreatedAt = old?.CreatedAt ?? default;
        input.Name = Rules.Name(input.Name);
        Rules.Require(input.InventoryMode is "prepared" or "untracked", "Invalid inventory mode.");
        Rules.Require(input.SellingPriceCentavos >= 0 && input.ManualCostCentavos >= 0, "Prices and costs cannot be negative.");
        Rules.Require(input.Recipe.Select(x => x.InventoryItemId).Distinct().Count() == input.Recipe.Count, "Recipe ingredients must be unique.");
        foreach (var line in input.Recipe)
        {
            Rules.Require(line.Quantity > 0, "Recipe quantity must be positive.");
            await repository.Required<InventoryItem>(line.InventoryItemId);
        }

        if (input.InventoryMode == "prepared")
        {
            Rules.Require(input.RecipeBatchYield > 0, "Reference batch yield must be positive.");
            Rules.Require(input.FinishedInventoryItemId.HasValue, "Prepared products require finished inventory.");
            await repository.Required<InventoryItem>(input.FinishedInventoryItemId!.Value);
            Rules.Require(!input.Recipe.Any(x => x.InventoryItemId == input.FinishedInventoryItemId), "A recipe cannot consume its own output.");
        }
        else
            input.RecipeBatchYield = 1;
        input.DeletedAt = null;
        await repository.Save(input);
    }

    private async Task Expense(Operation op, bool delete)
    {
        var old = await repository.Find<Expense>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        var value = delete ? old ?? throw new DomainException("not_found", "Expense unavailable.", 404) : Input<Expense>(op);
        value.Id = op.EntityId;
        value.CreatedAt = old?.CreatedAt ?? default;
        if (delete)
            value.DeletedAt = DateTimeOffset.UtcNow;
        else
        {
            value.Description = Rules.Name(value.Description);
            value.Category = Rules.Name(value.Category);
            Rules.Require(value.AmountCentavos >= 0, "Expense cannot be negative.");
            Date(value.ExpenseDate);
            value.DeletedAt = null;
        }

        await repository.Save(value);
    }

    private static void Date(string value)
    {
        Rules.Require(DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date), "Use a valid YYYY-MM-DD business date.");
        Rules.Require(date <= Calculations.Today(), "Sales and expenses cannot have a future date.");
    }

    private async Task New<T>(Guid id)
        where T : Entity
    {
        if (await repository.Find<T>(id)is not null)
            throw new DomainException("conflict", "This transaction already exists under another operation.", 409);
    }

    private async Task Move(InventoryItem item, decimal delta, long unitCost, string type, string reference, Guid referenceId, string? note)
    {
        item.CurrentQuantity += delta;
        await repository.Save(item);
        await repository.Save(new InventoryMovement { Id = Guid.NewGuid(), InventoryItemId = item.Id, ItemName = item.Name, QuantityDelta = delta, BalanceAfter = item.CurrentQuantity, UnitCostCentavos = unitCost, MovementType = type, ReferenceType = reference, ReferenceId = referenceId, Note = note });
    }

    private async Task Receive(Operation op)
    {
        await New<StockReceipt>(op.EntityId);
        var input = Input<ReceiptInput>(op);
        Rules.Require(input.Quantity > 0 && input.ToBase > 0 && input.TotalCostCentavos >= 0, "Invalid stock receipt.");
        var item = await repository.Required<InventoryItem>(input.InventoryItemId);
        var factors = new Dictionary<string, (string Unit, decimal Factor)>
        {
            ["kg"] = ("g", 1000),
            ["g"] = ("g", 1),
            ["L"] = ("ml", 1000),
            ["ml"] = ("ml", 1)
        };
        var valid = input.Unit == item.BaseUnit && input.ToBase == 1 || factors.TryGetValue(input.Unit, out var conversion) && conversion.Unit == item.BaseUnit && conversion.Factor == input.ToBase;
        Rules.Require(valid, "The unit conversion does not match the inventory base unit.");
        var quantity = input.Quantity * input.ToBase;
        item.AverageCostCentavos = Rules.Average(item.CurrentQuantity, item.AverageCostCentavos, quantity, input.TotalCostCentavos);
        var receipt = new StockReceipt
        {
            Id = op.EntityId,
            InventoryItemId = item.Id,
            ItemName = item.Name,
            Quantity = input.Quantity,
            Unit = input.Unit,
            QuantityInBaseUnit = quantity,
            TotalCostCentavos = input.TotalCostCentavos,
            ResultingAverageCostCentavos = item.AverageCostCentavos,
            ReceivedAt = DateTimeOffset.UtcNow,
            Note = input.Note
        };
        await repository.Save(receipt);
        await Move(item, quantity, item.AverageCostCentavos, "purchase", "stock_receipt", receipt.Id, input.Note);
    }

    private async Task Adjust(Operation op)
    {
        var input = Input<AdjustmentInput>(op);
        var item = await repository.Required<InventoryItem>(input.InventoryItemId);
        CheckVersion(item, op.ExpectedVersion);
        Rules.Require(input.CountedQuantity >= 0 && !string.IsNullOrWhiteSpace(input.Reason), "Provide a nonnegative count and reason.");
        await Move(item, input.CountedQuantity - item.CurrentQuantity, item.AverageCostCentavos, input.Reason == "Waste / spoilage" ? "waste" : "adjustment", "adjustment", op.EntityId, input.Reason + " · " + input.Note);
    }

    private async Task Sale(Operation op)
    {
        await New<Sale>(op.EntityId);
        var input = Input<SaleInput>(op);
        Date(input.SaleDate);
        var sale = new Sale
        {
            Id = op.EntityId,
            SaleDate = input.SaleDate,
            Location = "",
            DeductInventory = input.DeductInventory
        };
        foreach (var(id, quantity)in input.Quantities)
        {
            Rules.Require(quantity >= 0, "Sale quantity cannot be negative.");
            if (quantity == 0)
                continue;
            var product = await repository.Required<Product>(id);
            Rules.Require(product.IsActive, "The product is inactive.");
            var cost = product.ManualCostCentavos;
            InventoryItem? finished = null;
            if (product.InventoryMode == "prepared")
            {
                finished = await repository.Required<InventoryItem>(product.FinishedInventoryItemId!.Value);
                cost = finished.AverageCostCentavos;
                if (cost <= 0)
                    foreach (var line in product.Recipe)
                        cost += Rules.Round(line.Quantity * (await repository.Required<InventoryItem>(line.InventoryItemId)).AverageCostCentavos);
            }

            var price = input.Prices?.GetValueOrDefault(id, product.SellingPriceCentavos) ?? product.SellingPriceCentavos;
            Rules.Require(price >= 0, "Sale prices cannot be negative.");
            sale.Lines.Add(new SaleLine { Id = Guid.NewGuid(), ProductId = id, ProductName = product.Name, Quantity = quantity, UnitPriceCentavos = price, UnitCostCentavos = cost, LineRevenueCentavos = Rules.Round(quantity * price), LineCostCentavos = Rules.Round(quantity * cost) });
            if (input.DeductInventory && finished is not null)
                await Move(finished, -quantity, cost, "sale_consumption", "sale", sale.Id, product.Name);
        }

        Rules.Require(sale.Lines.Count > 0, "Enter at least one quantity.");
        sale.TotalItems = sale.Lines.Sum(x => x.Quantity);
        sale.TotalRevenueCentavos = sale.Lines.Sum(x => x.LineRevenueCentavos);
        sale.TotalCostCentavos = sale.Lines.Sum(x => x.LineCostCentavos);
        sale.TotalProfitCentavos = sale.TotalRevenueCentavos - sale.TotalCostCentavos;
        await repository.Save(sale);
    }

    private async Task Batch(Operation op)
    {
        var input = Input<BatchInput>(op);
        var old = await repository.Find<ProductionBatch>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        Rules.Require(old?.Status != "completed", "Completed batches are immutable.");
        Rules.Require(input.PlannedYield > 0, "Planned yield must be positive.");
        var product = await repository.Required<Product>(input.ProductId);
        Rules.Require(product.InventoryMode == "prepared", "Select a prepared product.");
        var batch = new ProductionBatch
        {
            Id = op.EntityId,
            CreatedAt = old?.CreatedAt ?? default,
            ProductId = product.Id,
            ProductName = product.Name,
            Status = "draft",
            PlannedYield = input.PlannedYield,
            Note = input.Note
        };
        foreach (var line in product.Recipe)
        {
            var item = await repository.Required<InventoryItem>(line.InventoryItemId);
            batch.Ingredients.Add(new BatchIngredient { InventoryItemId = item.Id, ItemName = item.Name, PlannedQuantity = line.Quantity * input.PlannedYield, ActualQuantity = line.Quantity * input.PlannedYield, UnitCostCentavos = item.AverageCostCentavos });
        }

        await repository.Save(batch);
    }

    private async Task Complete(Operation op)
    {
        var input = Input<CompleteInput>(op);
        var batch = await repository.Required<ProductionBatch>(op.EntityId);
        CheckVersion(batch, op.ExpectedVersion);
        Rules.Require(batch.Status == "draft" && input.ActualYield > 0, "A draft and positive actual yield are required.");
        var product = await repository.Required<Product>(batch.ProductId);
        Rules.Require(product.FinishedInventoryItemId.HasValue, "Finished stock is missing.");
        long total = 0;
        foreach (var line in batch.Ingredients)
        {
            line.ActualQuantity = input.ActualQuantities.GetValueOrDefault(line.InventoryItemId, line.ActualQuantity);
            Rules.Require(line.ActualQuantity >= 0, "Ingredient quantities cannot be negative.");
            var item = await repository.Required<InventoryItem>(line.InventoryItemId);
            line.UnitCostCentavos = item.AverageCostCentavos;
            total += Rules.Round(line.ActualQuantity * line.UnitCostCentavos);
            await Move(item, -line.ActualQuantity, line.UnitCostCentavos, "batch_consumption", "production_batch", batch.Id, batch.ProductName);
        }

        var finished = await repository.Required<InventoryItem>(product.FinishedInventoryItemId!.Value);
        finished.AverageCostCentavos = Rules.Average(finished.CurrentQuantity, finished.AverageCostCentavos, input.ActualYield, total);
        await Move(finished, input.ActualYield, Rules.Round(total / input.ActualYield), "batch_output", "production_batch", batch.Id, batch.ProductName);
        batch.Status = "completed";
        batch.ActualYield = input.ActualYield;
        batch.TotalCostCentavos = total;
        batch.CostPerUnitCentavos = Rules.Round(total / input.ActualYield);
        batch.CompletedAt = DateTimeOffset.UtcNow;
        batch.Note = input.Note ?? batch.Note;
        await repository.Save(batch);
    }
}
