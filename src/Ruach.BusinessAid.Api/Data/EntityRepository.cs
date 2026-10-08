using Dapper;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Ruach.BusinessAid.Shared;

namespace Ruach.BusinessAid.Api.Data;

// Identifiers below are compile-time allowlisted SQL, never supplied by a client.
public sealed class EntityRepository(SqlConnection connection, SqlTransaction transaction, Guid business) : IEntityRepository
{
    public async Task<T?> Find<T>(Guid id) where T : Entity
    {
        var table = Table(typeof(T));
        var columns = typeof(T) == typeof(Sale) ? "*,CONVERT(varchar(10),SaleDate,23) SaleDate" : typeof(T) == typeof(Expense) ? "*,CONVERT(varchar(10),ExpenseDate,23) ExpenseDate" : "*";
        var value = await connection.QuerySingleOrDefaultAsync<T>($"SELECT {columns} FROM dbo.{table} WHERE BusinessId=@business AND Id=@id", new { business, id }, transaction);
        if (value is Sale sale)
            sale.Lines = (await connection.QueryAsync<SaleLine>("SELECT * FROM dbo.ItemSaleLines WHERE BusinessId=@business AND SaleId=@id", new { business, id }, transaction)).ToList();
        return value;
    }

    public async Task<T> Required<T>(Guid id) where T : Entity =>
        (await Find<T>(id)) is { DeletedAt: null } value ? value : throw new DomainException("not_found", "The referenced record is unavailable.", 404);

    public static void CheckVersion(Entity? current, string? expected) => Rules.CheckVersion(current, expected);

    public async Task Save(Entity value)
    {
        value.BusinessId = business;
        value.UpdatedAt = DateTimeOffset.UtcNow;
        if (value.CreatedAt == default) value.CreatedAt = value.UpdatedAt;
        var (insert, update) = Statements(value.GetType());
        var args = new DynamicParameters(value);
        var exists = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{Table(value.GetType())} WHERE BusinessId=@BusinessId AND Id=@Id", args, transaction);
        value.Version = await connection.QuerySingleAsync<byte[]>(exists == 0 ? insert : update, args, transaction);
        if (value is Sale sale && exists == 0)
            foreach (var line in sale.Lines)
                await connection.ExecuteAsync("INSERT dbo.ItemSaleLines(BusinessId,SaleId,Id,ItemId,ItemName,Quantity,UnitPriceCentavos,UnitCostCentavos,LineRevenueCentavos,LineCostCentavos) VALUES(@business,@saleId,@Id,@ItemId,@ItemName,@Quantity,@UnitPriceCentavos,@UnitCostCentavos,@LineRevenueCentavos,@LineCostCentavos)", new { business, saleId = sale.Id, line.Id, line.ItemId, line.ItemName, line.Quantity, line.UnitPriceCentavos, line.UnitCostCentavos, line.LineRevenueCentavos, line.LineCostCentavos }, transaction);
        await Log(LocalData.Collection(value), value.Id, value);
    }

    public Task Log(string collection, Guid id, object value) => connection.ExecuteAsync(
        "INSERT dbo.ItemSyncChanges(BusinessId,CollectionName,EntityId,Payload) VALUES(@business,@collection,@id,@payload)",
        new { business, collection, id, payload = JsonSerializer.Serialize(value, value.GetType(), Wire.Json) }, transaction);

    public async Task<AppData> Load()
    {
        using var rows = await connection.QueryMultipleAsync("""
            SELECT * FROM dbo.Businesses WHERE Id=@business;
            SELECT * FROM dbo.Items WHERE BusinessId=@business;
            SELECT *,CONVERT(varchar(10),SaleDate,23) SaleDate FROM dbo.ItemSales WHERE BusinessId=@business;
            SELECT *,CONVERT(varchar(10),ExpenseDate,23) ExpenseDate FROM dbo.BusinessExpenses WHERE BusinessId=@business;
            SELECT * FROM dbo.StockEntries WHERE BusinessId=@business;
            SELECT * FROM dbo.StockMovements WHERE BusinessId=@business;
            SELECT * FROM dbo.ItemSaleLines WHERE BusinessId=@business;
            """, new { business }, transaction);
        var data = new AppData
        {
            Business = await rows.ReadSingleOrDefaultAsync<Business>(),
            Items = (await rows.ReadAsync<Item>()).ToList(),
            Sales = (await rows.ReadAsync<Sale>()).ToList(),
            Expenses = (await rows.ReadAsync<Expense>()).ToList(),
            Receipts = (await rows.ReadAsync<StockReceipt>()).ToList(),
            Movements = (await rows.ReadAsync<StockMovement>()).ToList()
        };
        var sales = (await rows.ReadAsync<SaleRow>()).ToLookup(x => x.SaleId);
        foreach (var sale in data.Sales) sale.Lines = sales[sale.Id].Cast<SaleLine>().ToList();
        return data;
    }

    private sealed class SaleRow : SaleLine { public Guid SaleId { get; set; } }

    public static string Table(Type type) => type.Name switch
    {
        nameof(Item) => "Items", nameof(Sale) => "ItemSales", nameof(Expense) => "BusinessExpenses",
        nameof(StockReceipt) => "StockEntries", nameof(StockMovement) => "StockMovements",
        _ => throw new InvalidOperationException("Unknown entity type")
    };

    private static (string Insert, string Update) Statements(Type type) => type.Name switch
    {
        nameof(Item) => ("INSERT dbo.Items(Id,BusinessId,Name,SellingPriceCentavos,UnitCostCentavos,CurrentQuantity,MinimumQuantity,IsActive,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@Name,@SellingPriceCentavos,@UnitCostCentavos,@CurrentQuantity,@MinimumQuantity,@IsActive,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.Items SET Name=@Name,SellingPriceCentavos=@SellingPriceCentavos,UnitCostCentavos=@UnitCostCentavos,CurrentQuantity=@CurrentQuantity,MinimumQuantity=@MinimumQuantity,IsActive=@IsActive,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(Sale) => ("INSERT dbo.ItemSales(Id,BusinessId,SaleDate,Location,TotalItems,TotalRevenueCentavos,TotalCostCentavos,TotalProfitCentavos,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@SaleDate,@Location,@TotalItems,@TotalRevenueCentavos,@TotalCostCentavos,@TotalProfitCentavos,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.ItemSales SET SaleDate=@SaleDate,Location=@Location,TotalItems=@TotalItems,TotalRevenueCentavos=@TotalRevenueCentavos,TotalCostCentavos=@TotalCostCentavos,TotalProfitCentavos=@TotalProfitCentavos,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(Expense) => ("INSERT dbo.BusinessExpenses(Id,BusinessId,Description,Category,AmountCentavos,ExpenseDate,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@Description,@Category,@AmountCentavos,@ExpenseDate,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.BusinessExpenses SET Description=@Description,Category=@Category,AmountCentavos=@AmountCentavos,ExpenseDate=@ExpenseDate,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(StockReceipt) => ("INSERT dbo.StockEntries(Id,BusinessId,ItemId,ItemName,Quantity,UnitCostCentavos,TotalCostCentavos,ReceivedAt,Note,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@ItemId,@ItemName,@Quantity,@UnitCostCentavos,@TotalCostCentavos,@ReceivedAt,@Note,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.StockEntries SET ItemId=@ItemId,ItemName=@ItemName,Quantity=@Quantity,UnitCostCentavos=@UnitCostCentavos,TotalCostCentavos=@TotalCostCentavos,ReceivedAt=@ReceivedAt,Note=@Note,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        nameof(StockMovement) => ("INSERT dbo.StockMovements(Id,BusinessId,ItemId,ItemName,QuantityDelta,BalanceAfter,UnitCostCentavos,MovementType,ReferenceType,ReferenceId,Note,CreatedAt,UpdatedAt,DeletedAt) OUTPUT INSERTED.Version VALUES(@Id,@BusinessId,@ItemId,@ItemName,@QuantityDelta,@BalanceAfter,@UnitCostCentavos,@MovementType,@ReferenceType,@ReferenceId,@Note,@CreatedAt,@UpdatedAt,@DeletedAt)", "UPDATE dbo.StockMovements SET ItemId=@ItemId,ItemName=@ItemName,QuantityDelta=@QuantityDelta,BalanceAfter=@BalanceAfter,UnitCostCentavos=@UnitCostCentavos,MovementType=@MovementType,ReferenceType=@ReferenceType,ReferenceId=@ReferenceId,Note=@Note,UpdatedAt=@UpdatedAt,DeletedAt=@DeletedAt OUTPUT INSERTED.Version WHERE BusinessId=@BusinessId AND Id=@Id"),
        _ => throw new InvalidOperationException("Unknown entity type")
    };
}
