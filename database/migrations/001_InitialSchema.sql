CREATE TABLE dbo.Users (
 Id uniqueidentifier NOT NULL PRIMARY KEY, GoogleSubject nvarchar(255) NOT NULL UNIQUE,
 DisplayName nvarchar(255) NOT NULL, Email nvarchar(320) NOT NULL, PhotoURL nvarchar(2048) NULL,
 CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE dbo.Businesses (
 Id uniqueidentifier NOT NULL PRIMARY KEY, OwnerUid uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.Users(Id),
 Name nvarchar(160) NOT NULL, DefaultLocation nvarchar(500) NOT NULL,
 Currency varchar(3) NOT NULL CHECK (Currency='PHP'), Timezone varchar(100) NOT NULL CHECK (Timezone='Asia/Manila'),
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL
);
CREATE TABLE dbo.BusinessMembers (
 BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id), UserId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
 Role varchar(20) NOT NULL CHECK(Role IN ('Owner','Manager','Staff')), PRIMARY KEY(BusinessId,UserId)
);
CREATE TABLE dbo.InventoryItems (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 Name nvarchar(160) NOT NULL,
 BaseUnit nvarchar(500) NOT NULL,
 UnitKind nvarchar(500) NOT NULL,
 CurrentQuantity decimal(19,6) NOT NULL,
 AverageCostCentavos bigint NOT NULL,
 MinimumQuantity decimal(19,6) NOT NULL,
 IsActive bit NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
ALTER TABLE dbo.InventoryItems ADD NormalizedName AS UPPER(LTRIM(RTRIM(Name))) COLLATE Latin1_General_100_CI_AS PERSISTED;
CREATE UNIQUE INDEX UX_InventoryItems_Name ON dbo.InventoryItems(BusinessId,NormalizedName) WHERE DeletedAt IS NULL;
CREATE TABLE dbo.Products (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 Name nvarchar(160) NOT NULL,
 SellingPriceCentavos bigint NOT NULL,
 ManualCostCentavos bigint NOT NULL,
 InventoryMode nvarchar(500) NOT NULL,
 FinishedInventoryItemId uniqueidentifier NULL,
 IsActive bit NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
ALTER TABLE dbo.Products ADD NormalizedName AS UPPER(LTRIM(RTRIM(Name))) COLLATE Latin1_General_100_CI_AS PERSISTED;
CREATE UNIQUE INDEX UX_Products_Name ON dbo.Products(BusinessId,NormalizedName) WHERE DeletedAt IS NULL;
CREATE TABLE dbo.Sales (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 SaleDate date NOT NULL,
 Location nvarchar(500) NOT NULL,
 TotalItems decimal(19,6) NOT NULL,
 TotalRevenueCentavos bigint NOT NULL,
 TotalCostCentavos bigint NOT NULL,
 TotalProfitCentavos bigint NOT NULL,
 DeductInventory bit NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
CREATE TABLE dbo.Expenses (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 Description nvarchar(500) NOT NULL,
 Category nvarchar(500) NOT NULL,
 AmountCentavos bigint NOT NULL,
 ExpenseDate date NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
CREATE TABLE dbo.StockReceipts (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 InventoryItemId uniqueidentifier NOT NULL,
 ItemName nvarchar(500) NOT NULL,
 Quantity decimal(19,6) NOT NULL,
 Unit nvarchar(500) NOT NULL,
 QuantityInBaseUnit decimal(19,6) NOT NULL,
 TotalCostCentavos bigint NOT NULL,
 ResultingAverageCostCentavos bigint NOT NULL,
 ReceivedAt datetimeoffset NOT NULL,
 Note nvarchar(500) NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
CREATE TABLE dbo.InventoryMovements (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 InventoryItemId uniqueidentifier NOT NULL,
 ItemName nvarchar(500) NOT NULL,
 QuantityDelta decimal(19,6) NOT NULL,
 BalanceAfter decimal(19,6) NOT NULL,
 UnitCostCentavos bigint NOT NULL,
 MovementType nvarchar(500) NOT NULL,
 ReferenceType nvarchar(500) NOT NULL,
 ReferenceId uniqueidentifier NOT NULL,
 Note nvarchar(500) NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);
CREATE TABLE dbo.ProductionBatches (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 ProductId uniqueidentifier NOT NULL,
 ProductName nvarchar(500) NOT NULL,
 Status nvarchar(500) NOT NULL,
 PlannedYield decimal(19,6) NOT NULL,
 ActualYield decimal(19,6) NULL,
 TotalCostCentavos bigint NULL,
 CostPerUnitCentavos bigint NULL,
 CompletedAt datetimeoffset NULL,
 Note nvarchar(500) NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id)
);

ALTER TABLE dbo.Products ADD FOREIGN KEY(BusinessId,FinishedInventoryItemId) REFERENCES dbo.InventoryItems(BusinessId,Id);
ALTER TABLE dbo.StockReceipts ADD FOREIGN KEY(BusinessId,InventoryItemId) REFERENCES dbo.InventoryItems(BusinessId,Id);
ALTER TABLE dbo.InventoryMovements ADD FOREIGN KEY(BusinessId,InventoryItemId) REFERENCES dbo.InventoryItems(BusinessId,Id);
ALTER TABLE dbo.ProductionBatches ADD FOREIGN KEY(BusinessId,ProductId) REFERENCES dbo.Products(BusinessId,Id);
ALTER TABLE dbo.Products ADD CHECK (InventoryMode IN ('prepared','untracked')), CHECK(SellingPriceCentavos>=0 AND ManualCostCentavos>=0);
ALTER TABLE dbo.InventoryItems ADD CHECK(AverageCostCentavos>=0 AND MinimumQuantity>=0);
ALTER TABLE dbo.StockReceipts ADD CHECK(Quantity>0 AND QuantityInBaseUnit>0 AND TotalCostCentavos>=0);
ALTER TABLE dbo.Expenses ADD CHECK(AmountCentavos>=0);
ALTER TABLE dbo.ProductionBatches ADD CHECK(Status IN ('draft','completed')), CHECK(PlannedYield>0);
CREATE TABLE dbo.RecipeLines (
 BusinessId uniqueidentifier NOT NULL, ProductId uniqueidentifier NOT NULL, InventoryItemId uniqueidentifier NOT NULL,
 Quantity decimal(19,6) NOT NULL CHECK(Quantity>0), PRIMARY KEY(BusinessId,ProductId,InventoryItemId),
 FOREIGN KEY(BusinessId,ProductId) REFERENCES dbo.Products(BusinessId,Id),
 FOREIGN KEY(BusinessId,InventoryItemId) REFERENCES dbo.InventoryItems(BusinessId,Id)
);
CREATE TABLE dbo.SaleLines (
 BusinessId uniqueidentifier NOT NULL, SaleId uniqueidentifier NOT NULL, Id uniqueidentifier NOT NULL,
 ProductId uniqueidentifier NOT NULL, ProductName nvarchar(160) NOT NULL, Quantity decimal(19,6) NOT NULL CHECK(Quantity>0),
 UnitPriceCentavos bigint NOT NULL CHECK(UnitPriceCentavos>=0), UnitCostCentavos bigint NOT NULL CHECK(UnitCostCentavos>=0),
 LineRevenueCentavos bigint NOT NULL, LineCostCentavos bigint NOT NULL,
 PRIMARY KEY(BusinessId,SaleId,Id), FOREIGN KEY(BusinessId,SaleId) REFERENCES dbo.Sales(BusinessId,Id),
 FOREIGN KEY(BusinessId,ProductId) REFERENCES dbo.Products(BusinessId,Id)
);
CREATE TABLE dbo.BatchIngredients (
 BusinessId uniqueidentifier NOT NULL, BatchId uniqueidentifier NOT NULL, InventoryItemId uniqueidentifier NOT NULL,
 ItemName nvarchar(160) NOT NULL, PlannedQuantity decimal(19,6) NOT NULL,
 ActualQuantity decimal(19,6) NOT NULL CHECK(ActualQuantity>=0), UnitCostCentavos bigint NOT NULL CHECK(UnitCostCentavos>=0),
 PRIMARY KEY(BusinessId,BatchId,InventoryItemId),
 FOREIGN KEY(BusinessId,BatchId) REFERENCES dbo.ProductionBatches(BusinessId,Id),
 FOREIGN KEY(BusinessId,InventoryItemId) REFERENCES dbo.InventoryItems(BusinessId,Id)
);
CREATE TABLE dbo.ProcessedOperations (
 BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id), Id uniqueidentifier NOT NULL,
 UserId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id), PayloadHash varchar(64) NOT NULL,
 CompletedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), PRIMARY KEY(BusinessId,Id)
);
CREATE TABLE dbo.SyncChanges (
 [Cursor] bigint IDENTITY NOT NULL PRIMARY KEY, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 CollectionName varchar(32) NOT NULL, EntityId uniqueidentifier NOT NULL, Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1),
 CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_SyncChanges_BusinessCursor ON dbo.SyncChanges(BusinessId,[Cursor]);
CREATE INDEX IX_Sales_Report ON dbo.Sales(BusinessId,SaleDate) INCLUDE(TotalRevenueCentavos,TotalCostCentavos,TotalItems) WHERE DeletedAt IS NULL;
CREATE INDEX IX_Expenses_Report ON dbo.Expenses(BusinessId,ExpenseDate) INCLUDE(AmountCentavos,Category) WHERE DeletedAt IS NULL;
CREATE INDEX IX_SaleLines_Product ON dbo.SaleLines(BusinessId,ProductId) INCLUDE(SaleId,Quantity,LineRevenueCentavos,LineCostCentavos);
