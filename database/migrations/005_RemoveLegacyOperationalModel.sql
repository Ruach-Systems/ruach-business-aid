-- Contract phase. Apply only after the simplified API/PWA and model reset have been verified.
-- Account, business, approval, and administration records are intentionally preserved.
DROP TABLE IF EXISTS dbo.BatchIngredients;
DROP TABLE IF EXISTS dbo.RecipeLines;
DROP TABLE IF EXISTS dbo.SaleLines;
DROP TABLE IF EXISTS dbo.StockReceipts;
DROP TABLE IF EXISTS dbo.InventoryMovements;
DROP TABLE IF EXISTS dbo.ProductionBatches;
DROP TABLE IF EXISTS dbo.Sales;
DROP TABLE IF EXISTS dbo.Expenses;
DROP TABLE IF EXISTS dbo.Products;
DROP TABLE IF EXISTS dbo.InventoryItems;
DROP TABLE IF EXISTS dbo.ProcessedOperations;
DROP TABLE IF EXISTS dbo.SyncChanges;
