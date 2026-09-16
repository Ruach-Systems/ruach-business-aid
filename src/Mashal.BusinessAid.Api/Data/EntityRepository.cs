using Dapper;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Mashal.BusinessAid.Shared;
namespace Mashal.BusinessAid.Api.Data;

// Identifiers below are compile-time allowlisted SQL, never supplied by a client.
public sealed class EntityRepository(SqlConnection connection, SqlTransaction transaction, Guid business) : IEntityRepository
{
    public async Task<T?> Find<T>(Guid id) where T : Entity
    {
        var table = Table(typeof(T));
        var columns = typeof(T) == typeof(Sale) ? "*,CONVERT(varchar(10),SaleDate,23) SaleDate" : typeof(T) == typeof(Expense) ? "*,CONVERT(varchar(10),ExpenseDate,23) ExpenseDate" : "*";
        var value = await connection.QuerySingleOrDefaultAsync<T>($"SELECT {columns} FROM dbo.{table} WHERE BusinessId=@business AND Id=@id", new { business, id }, transaction);
        if (value is Product p) p.Recipe = (await connection.QueryAsync<RecipeLine>("SELECT * FROM dbo.RecipeLines WHERE BusinessId=@business AND ProductId=@id", new { business, id }, transaction)).ToList();
        if (value is Sale s) s.Lines = (await connection.QueryAsync<SaleLine>("SELECT * FROM dbo.SaleLines WHERE BusinessId=@business AND SaleId=@id", new { business, id }, transaction)).ToList();
        if (value is ProductionBatch b) b.Ingredients = (await connection.QueryAsync<BatchIngredient>("SELECT * FROM dbo.BatchIngredients WHERE BusinessId=@business AND BatchId=@id", new { business, id }, transaction)).ToList();
        return value;
    }
    public async Task<T> Required<T>(Guid id) where T : Entity =>
     (await Find<T>(id)) is { DeletedAt: null } value ? value : throw new DomainException("not_found", "The referenced record is unavailable.", 404);
    public static void CheckVersion(Entity? current, string? expected)
    {
        Rules.CheckVersion(current, expected);
    }
    public async Task Save(Entity value)
    {
        value.BusinessId = business;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        if (value.CreatedAt == default) value.CreatedAt = value.UpdatedAt;
        var (insert, update) = Statements(value.GetType());
        var args = new DynamicParameters(value);
        var exists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{Table(value.GetType())} WHERE BusinessId=@BusinessId AND Id=@Id", args, transaction);
        value.Version = await connection.QuerySingleAsync<byte[]>(exists == 0 ? insert : update, args, transaction);
        if (value is Product p)
        {
            await connection.ExecuteAsync("DELETE dbo.RecipeLines WHERE BusinessId=@business AND ProductId=@Id", new { business, p.Id }, transaction);
            foreach (var line in p.Recipe) await connection.ExecuteAsync("INSERT dbo.RecipeLines(BusinessId,ProductId,InventoryItemId,Quantity) VALUES(@business,@Id,@InventoryItemId,@Quantity)", new { business, p.Id, line.InventoryItemId, line.Quantity }, transaction);
        }
        if (value is Sale s && exists == 0) foreach (var l in s.Lines) await connection.ExecuteAsync("INSERT dbo.SaleLines(BusinessId,SaleId,Id,ProductId,ProductName,Quantity,UnitPriceCentavos,UnitCostCentavos,LineRevenueCentavos,LineCostCentavos) VALUES(@business,@saleId,@Id,@ProductId,@ProductName,@Quantity,@UnitPriceCentavos,@UnitCostCentavos,@LineRevenueCentavos,@LineCostCentavos)", new { business, saleId = s.Id, l.Id, l.ProductId, l.ProductName, l.Quantity, l.UnitPriceCentavos, l.UnitCostCentavos, l.LineRevenueCentavos, l.LineCostCentavos }, transaction);
        if (value is ProductionBatch b)
        {
            await connection.ExecuteAsync("DELETE dbo.BatchIngredients WHERE BusinessId=@business AND BatchId=@Id", new { business, b.Id }, transaction);
            foreach (var l in b.Ingredients) await connection.ExecuteAsync("INSERT dbo.BatchIngredients(BusinessId,BatchId,InventoryItemId,ItemName,PlannedQuantity,ActualQuantity,UnitCostCentavos) VALUES(@business,@Id,@InventoryItemId,@ItemName,@PlannedQuantity,@ActualQuantity,@UnitCostCentavos)", new { business, b.Id, l.InventoryItemId, l.ItemName, l.PlannedQuantity, l.ActualQuantity, l.UnitCostCentavos }, transaction);
        }
        await Log(Collection(value.GetType()), value.Id, value);
    }
    public Task Log(string collection, Guid id, object value) => connection.ExecuteAsync(
     "INSERT dbo.SyncChanges(BusinessId,CollectionName,EntityId,Payload) VALUES(@business,@collection,@id,@payload)",
     new { business, collection, id, payload = JsonSerializer.Serialize(value, value.GetType(), Wire.Json) }, transaction);
    public async Task<AppData> Load()
    {
        // One round trip for the initial snapshot; child rows remain normalized in SQL.
        using var rows = await connection.QueryMultipleAsync("""
            SELECT * FROM dbo.Businesses WHERE Id=@business;
            SELECT * FROM dbo.InventoryItems WHERE BusinessId=@business;
            SELECT * FROM dbo.Products WHERE BusinessId=@business;
            SELECT *,CONVERT(varchar(10),SaleDate,23) SaleDate FROM dbo.Sales WHERE BusinessId=@business;
            SELECT *,CONVERT(varchar(10),ExpenseDate,23) ExpenseDate FROM dbo.Expenses WHERE BusinessId=@business;
            SELECT * FROM dbo.StockReceipts WHERE BusinessId=@business;
            SELECT * FROM dbo.InventoryMovements WHERE BusinessId=@business;
            SELECT * FROM dbo.ProductionBatches WHERE BusinessId=@business;
            SELECT * FROM dbo.RecipeLines WHERE BusinessId=@business;
            SELECT * FROM dbo.SaleLines WHERE BusinessId=@business;
            SELECT * FROM dbo.BatchIngredients WHERE BusinessId=@business;
            """, new { business }, transaction);
        var data = new AppData
        {
            Business = await rows.ReadSingleOrDefaultAsync<Business>(),
            InventoryItems = (await rows.ReadAsync<InventoryItem>()).ToList(),
            Products = (await rows.ReadAsync<Product>()).ToList(),
            Sales = (await rows.ReadAsync<Sale>()).ToList(),
            Expenses = (await rows.ReadAsync<Expense>()).ToList(),
            Receipts = (await rows.ReadAsync<StockReceipt>()).ToList(),
            Movements = (await rows.ReadAsync<InventoryMovement>()).ToList(),
            Batches = (await rows.ReadAsync<ProductionBatch>()).ToList()
        };
        var recipes = (await rows.ReadAsync<RecipeRow>()).ToLookup(x => x.ProductId);
        var sales = (await rows.ReadAsync<SaleRow>()).ToLookup(x => x.SaleId);
        var batches = (await rows.ReadAsync<IngredientRow>()).ToLookup(x => x.BatchId);
        foreach (var product in data.Products) product.Recipe = recipes[product.Id].Cast<RecipeLine>().ToList();
        foreach (var sale in data.Sales) sale.Lines = sales[sale.Id].Cast<SaleLine>().ToList();
        foreach (var batch in data.Batches) batch.Ingredients = batches[batch.Id].Cast<BatchIngredient>().ToList();
        return data;
    }
    private sealed class RecipeRow : RecipeLine { public Guid ProductId { get; set; } }
    private sealed class SaleRow : SaleLine { public Guid SaleId { get; set; } }
    private sealed class IngredientRow : BatchIngredient { public Guid BatchId { get; set; } }
    public static string Table(Type type) => type.Name switch
    {
        nameof(InventoryItem) => "InventoryItems",
        nameof(Product) => "Products",
        nameof(Sale) => "Sales",
        nameof(Expense) => "Expenses",
        nameof(StockReceipt) => "StockReceipts",
        nameof(InventoryMovement) => "InventoryMovements",
        nameof(ProductionBatch) => "ProductionBatches",
        _ => throw new InvalidOperationException("Unknown entity type")
    };
    public static string Collection(Type type) => type.Name switch
    {
        nameof(InventoryItem) => "inventoryItems",
        nameof(Product) => "products",
        nameof(Sale) => "sales",
        nameof(Expense) => "expenses",
        nameof(StockReceipt) => "receipts",
        nameof(InventoryMovement) => "movements",
        nameof(ProductionBatch) => "batches",
        _ => throw new InvalidOperationException("Unknown entity type")
    };
    private static (string Insert, string Update) Statements(Type type) => type.Name switch
    {
        nameof(InventoryItem) => ("INSERT dbo.InventoryItems(Id,BusinessId,Name,BaseUnit,UnitKind,CurrentQuantity,AverageCostCentavos,MinimumQuantity,IsActive,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@Name,@BaseUnit,@UnitKind,@CurrentQuantity,@AverageCostCentavos,@MinimumQuantity,@IsActive,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.InventoryItems SET Name=@Name,BaseUnit=@BaseUnit,UnitKind=@UnitKind,CurrentQuantity=@CurrentQuantity,AverageCostCentavos=@AverageCostCentavos,MinimumQuantity=@MinimumQuantity,IsActive=@IsActive,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(Product) => ("INSERT dbo.Products(Id,BusinessId,Name,SellingPriceCentavos,ManualCostCentavos,InventoryMode,FinishedInventoryItemId,RecipeBatchYield,IsActive,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@Name,@SellingPriceCentavos,@ManualCostCentavos,@InventoryMode,@FinishedInventoryItemId,@RecipeBatchYield,@IsActive,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.Products SET Name=@Name,SellingPriceCentavos=@SellingPriceCentavos,ManualCostCentavos=@ManualCostCentavos,InventoryMode=@InventoryMode,FinishedInventoryItemId=@FinishedInventoryItemId,RecipeBatchYield=@RecipeBatchYield,IsActive=@IsActive,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(Sale) => ("INSERT dbo.Sales(Id,BusinessId,SaleDate,Location,TotalItems,TotalRevenueCentavos,TotalCostCentavos,TotalProfitCentavos,DeductInventory,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@SaleDate,@Location,@TotalItems,@TotalRevenueCentavos,@TotalCostCentavos,@TotalProfitCentavos,@DeductInventory,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.Sales SET SaleDate=@SaleDate,Location=@Location,TotalItems=@TotalItems,TotalRevenueCentavos=@TotalRevenueCentavos,TotalCostCentavos=@TotalCostCentavos,TotalProfitCentavos=@TotalProfitCentavos,DeductInventory=@DeductInventory,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(Expense) => ("INSERT dbo.Expenses(Id,BusinessId,Description,Category,AmountCentavos,ExpenseDate,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@Description,@Category,@AmountCentavos,@ExpenseDate,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.Expenses SET Description=@Description,Category=@Category,AmountCentavos=@AmountCentavos,ExpenseDate=@ExpenseDate,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(StockReceipt) => ("INSERT dbo.StockReceipts(Id,BusinessId,InventoryItemId,ItemName,Quantity,Unit,QuantityInBaseUnit,TotalCostCentavos,ResultingAverageCostCentavos,ReceivedAt,Note,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@InventoryItemId,@ItemName,@Quantity,@Unit,@QuantityInBaseUnit,@TotalCostCentavos,@ResultingAverageCostCentavos,@ReceivedAt,@Note,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.StockReceipts SET InventoryItemId=@InventoryItemId,ItemName=@ItemName,Quantity=@Quantity,Unit=@Unit,QuantityInBaseUnit=@QuantityInBaseUnit,TotalCostCentavos=@TotalCostCentavos,ResultingAverageCostCentavos=@ResultingAverageCostCentavos,ReceivedAt=@ReceivedAt,Note=@Note,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(InventoryMovement) => ("INSERT dbo.InventoryMovements(Id,BusinessId,InventoryItemId,ItemName,QuantityDelta,BalanceAfter,UnitCostCentavos,MovementType,ReferenceType,ReferenceId,Note,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@InventoryItemId,@ItemName,@QuantityDelta,@BalanceAfter,@UnitCostCentavos,@MovementType,@ReferenceType,@ReferenceId,@Note,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.InventoryMovements SET InventoryItemId=@InventoryItemId,ItemName=@ItemName,QuantityDelta=@QuantityDelta,BalanceAfter=@BalanceAfter,UnitCostCentavos=@UnitCostCentavos,MovementType=@MovementType,ReferenceType=@ReferenceType,ReferenceId=@ReferenceId,Note=@Note,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(ProductionBatch) => ("INSERT dbo.ProductionBatches(Id,BusinessId,ProductId,ProductName,Status,PlannedYield,ActualYield,TotalCostCentavos,CostPerUnitCentavos,CompletedAt,Note,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@ProductId,@ProductName,@Status,@PlannedYield,@ActualYield,@TotalCostCentavos,@CostPerUnitCentavos,@CompletedAt,@Note,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.ProductionBatches SET ProductId=@ProductId,ProductName=@ProductName,Status=@Status,PlannedYield=@PlannedYield,ActualYield=@ActualYield,TotalCostCentavos=@TotalCostCentavos,CostPerUnitCentavos=@CostPerUnitCentavos,CompletedAt=@CompletedAt,Note=@Note,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        _ => throw new InvalidOperationException("Unknown entity type")
    };
}
