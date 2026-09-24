namespace Mashal.BusinessAid.Shared;

// Each handler receives a business-scoped repository within the push transaction.
public sealed class CommandProcessor(IEntityRepository repository, bool checkConcurrency = true)
{
    private void CheckVersion(Entity? current, string? expected)
    {
        if (checkConcurrency) Rules.CheckVersion(current, expected);
    }

    public async Task Execute(Operation op)
    {
        Rules.Require(op.EntityId != Guid.Empty && op.Id != Guid.Empty, "Operation and entity IDs are required.");
        switch (op.Type)
        {
            case "saveItem": await SaveItem(op); break;
            case "saveExpense": await Expense(op, false); break;
            case "deleteExpense": await Expense(op, true); break;
            case "recordSale": await Sale(op); break;
            case "receiveStock": await Receive(op); break;
            case "adjustStock": await Adjust(op); break;
            default: throw new DomainException("validation", "Unknown operation type.");
        }
    }

    private static T Input<T>(Operation op) => Wire.Read<T>(op.Payload);

    private async Task SaveItem(Operation op)
    {
        var input = Input<Item>(op);
        var old = await repository.Find<Item>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        var item = old ?? new Item { Id = op.EntityId };
        item.Name = Rules.Name(input.Name);
        item.SellingPriceCentavos = input.SellingPriceCentavos;
        item.UnitCostCentavos = input.UnitCostCentavos;
        item.MinimumQuantity = input.MinimumQuantity;
        item.IsActive = input.IsActive;
        item.DeletedAt = null;
        Rules.Require(item.SellingPriceCentavos > 0, "Selling price must be greater than zero.");
        Rules.Require(item.UnitCostCentavos >= 0, "Unit cost cannot be negative.");
        Rules.Require(item.MinimumQuantity >= 0, "Low-stock warning cannot be negative.");
        await repository.Save(item);
    }

    private async Task Expense(Operation op, bool delete)
    {
        var old = await repository.Find<Expense>(op.EntityId);
        CheckVersion(old, op.ExpectedVersion);
        var value = delete ? old ?? throw new DomainException("not_found", "Expense unavailable.", 404) : Input<Expense>(op);
        value.Id = op.EntityId;
        value.CreatedAt = old?.CreatedAt ?? default;
        if (delete) value.DeletedAt = DateTimeOffset.UtcNow;
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

    private async Task New<T>(Guid id) where T : Entity
    {
        if (await repository.Find<T>(id) is not null)
            throw new DomainException("conflict", "This transaction already exists under another operation.", 409);
    }

    private async Task Move(Item item, long delta, long unitCost, string type, string reference, Guid referenceId, string? note)
    {
        item.CurrentQuantity = checked(item.CurrentQuantity + delta);
        await repository.Save(item);
        await repository.Save(new StockMovement
        {
            Id = Guid.NewGuid(), ItemId = item.Id, ItemName = item.Name, QuantityDelta = delta,
            BalanceAfter = item.CurrentQuantity, UnitCostCentavos = unitCost, MovementType = type,
            ReferenceType = reference, ReferenceId = referenceId, Note = note
        });
    }

    private async Task Receive(Operation op)
    {
        await New<StockReceipt>(op.EntityId);
        var input = Input<ReceiptInput>(op);
        Rules.Require(input.Quantity > 0, "Enter a positive whole-number stock quantity.");
        var item = await repository.Required<Item>(input.ItemId);
        var receipt = new StockReceipt
        {
            Id = op.EntityId, ItemId = item.Id, ItemName = item.Name, Quantity = input.Quantity,
            UnitCostCentavos = item.UnitCostCentavos,
            TotalCostCentavos = checked(input.Quantity * item.UnitCostCentavos),
            ReceivedAt = DateTimeOffset.UtcNow, Note = input.Note
        };
        await repository.Save(receipt);
        await Move(item, input.Quantity, item.UnitCostCentavos, "stock_added", "stock_receipt", receipt.Id, input.Note);
    }

    private async Task Adjust(Operation op)
    {
        var input = Input<AdjustmentInput>(op);
        var item = await repository.Required<Item>(input.ItemId);
        CheckVersion(item, op.ExpectedVersion);
        Rules.Require(input.CountedQuantity >= 0 && !string.IsNullOrWhiteSpace(input.Reason), "Provide a nonnegative whole-number count and reason.");
        await Move(item, checked(input.CountedQuantity - item.CurrentQuantity), item.UnitCostCentavos,
            input.Reason == "Waste / spoilage" ? "waste" : "stock_correction", "adjustment", op.EntityId,
            input.Reason + (string.IsNullOrWhiteSpace(input.Note) ? "" : " · " + input.Note));
    }

    private async Task Sale(Operation op)
    {
        await New<Sale>(op.EntityId);
        var input = Input<SaleInput>(op);
        Date(input.SaleDate);
        Rules.Require(input.Lines.Count > 0, "Enter at least one quantity.");
        Rules.Require(input.Lines.Select(x => x.ItemId).Distinct().Count() == input.Lines.Count, "Each item may appear only once.");
        var sale = new Sale { Id = op.EntityId, SaleDate = input.SaleDate, Location = "" };
        foreach (var line in input.Lines)
        {
            Rules.Require(line.Quantity > 0, "Sale quantities must be positive whole numbers.");
            var item = await repository.Required<Item>(line.ItemId);
            Rules.Require(item.IsActive, "The item is inactive.");
            sale.Lines.Add(new SaleLine
            {
                Id = Guid.NewGuid(), ItemId = item.Id, ItemName = item.Name, Quantity = line.Quantity,
                UnitPriceCentavos = item.SellingPriceCentavos, UnitCostCentavos = item.UnitCostCentavos,
                LineRevenueCentavos = checked(line.Quantity * item.SellingPriceCentavos),
                LineCostCentavos = checked(line.Quantity * item.UnitCostCentavos)
            });
            await Move(item, -line.Quantity, item.UnitCostCentavos, "sale", "sale", sale.Id, item.Name);
        }
        sale.TotalItems = sale.Lines.Sum(x => x.Quantity);
        sale.TotalRevenueCentavos = sale.Lines.Sum(x => x.LineRevenueCentavos);
        sale.TotalCostCentavos = sale.Lines.Sum(x => x.LineCostCentavos);
        sale.TotalProfitCentavos = sale.TotalRevenueCentavos - sale.TotalCostCentavos;
        await repository.Save(sale);
    }
}
