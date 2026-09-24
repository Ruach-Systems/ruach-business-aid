-- Expand phase for the simplified sales aid. Existing identity, business, and legacy
-- operational tables remain untouched until 005 is applied after application cutover.
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Businesses', N'U') IS NULL
    THROW 50000, 'Apply the initial schema before migration 004.', 1;

CREATE TABLE dbo.Items (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 Name nvarchar(160) NOT NULL,
 SellingPriceCentavos bigint NOT NULL,
 UnitCostCentavos bigint NOT NULL,
 CurrentQuantity bigint NOT NULL CONSTRAINT DF_Items_CurrentQuantity DEFAULT 0,
 MinimumQuantity bigint NOT NULL CONSTRAINT DF_Items_MinimumQuantity DEFAULT 0,
 IsActive bit NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id),
 CONSTRAINT CK_Items_Money CHECK(SellingPriceCentavos>0 AND UnitCostCentavos>=0),
 CONSTRAINT CK_Items_Minimum CHECK(MinimumQuantity>=0)
);
ALTER TABLE dbo.Items ADD NormalizedName AS UPPER(LTRIM(RTRIM(Name))) COLLATE Latin1_General_100_CI_AS PERSISTED;
CREATE UNIQUE INDEX UX_Items_Name ON dbo.Items(BusinessId,NormalizedName) WHERE DeletedAt IS NULL;

CREATE TABLE dbo.ItemSales (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 SaleDate date NOT NULL, Location nvarchar(500) NOT NULL,
 TotalItems bigint NOT NULL, TotalRevenueCentavos bigint NOT NULL,
 TotalCostCentavos bigint NOT NULL, TotalProfitCentavos bigint NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id),
 CONSTRAINT CK_ItemSales_Totals CHECK(TotalItems>0 AND TotalRevenueCentavos>=0 AND TotalCostCentavos>=0)
);

CREATE TABLE dbo.ItemSaleLines (
 BusinessId uniqueidentifier NOT NULL, SaleId uniqueidentifier NOT NULL, Id uniqueidentifier NOT NULL,
 ItemId uniqueidentifier NOT NULL, ItemName nvarchar(160) NOT NULL, Quantity bigint NOT NULL,
 UnitPriceCentavos bigint NOT NULL, UnitCostCentavos bigint NOT NULL,
 LineRevenueCentavos bigint NOT NULL, LineCostCentavos bigint NOT NULL,
 PRIMARY KEY(BusinessId,SaleId,Id),
 FOREIGN KEY(BusinessId,SaleId) REFERENCES dbo.ItemSales(BusinessId,Id),
 FOREIGN KEY(BusinessId,ItemId) REFERENCES dbo.Items(BusinessId,Id),
 CONSTRAINT CK_ItemSaleLines_Values CHECK(Quantity>0 AND UnitPriceCentavos>=0 AND UnitCostCentavos>=0 AND LineRevenueCentavos>=0 AND LineCostCentavos>=0)
);

CREATE TABLE dbo.BusinessExpenses (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 Description nvarchar(500) NOT NULL, Category nvarchar(500) NOT NULL,
 AmountCentavos bigint NOT NULL, ExpenseDate date NOT NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id),
 CONSTRAINT CK_BusinessExpenses_Amount CHECK(AmountCentavos>=0)
);

CREATE TABLE dbo.StockEntries (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 ItemId uniqueidentifier NOT NULL, ItemName nvarchar(160) NOT NULL,
 Quantity bigint NOT NULL, UnitCostCentavos bigint NOT NULL, TotalCostCentavos bigint NOT NULL,
 ReceivedAt datetimeoffset NOT NULL, Note nvarchar(500) NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id),
 FOREIGN KEY(BusinessId,ItemId) REFERENCES dbo.Items(BusinessId,Id),
 CONSTRAINT CK_StockEntries_Values CHECK(Quantity>0 AND UnitCostCentavos>=0 AND TotalCostCentavos>=0)
);

CREATE TABLE dbo.StockMovements (
 Id uniqueidentifier NOT NULL, BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 ItemId uniqueidentifier NOT NULL, ItemName nvarchar(160) NOT NULL,
 QuantityDelta bigint NOT NULL, BalanceAfter bigint NOT NULL, UnitCostCentavos bigint NOT NULL,
 MovementType nvarchar(40) NOT NULL, ReferenceType nvarchar(40) NOT NULL,
 ReferenceId uniqueidentifier NOT NULL, Note nvarchar(500) NULL,
 CreatedAt datetimeoffset NOT NULL, UpdatedAt datetimeoffset NOT NULL, DeletedAt datetimeoffset NULL,
 Version rowversion NOT NULL, PRIMARY KEY(BusinessId,Id),
 FOREIGN KEY(BusinessId,ItemId) REFERENCES dbo.Items(BusinessId,Id),
 CONSTRAINT CK_StockMovements_Cost CHECK(UnitCostCentavos>=0)
);

CREATE TABLE dbo.ItemProcessedOperations (
 BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id), Id uniqueidentifier NOT NULL,
 UserId uniqueidentifier NOT NULL REFERENCES dbo.Users(Id), PayloadHash varchar(64) NOT NULL,
 CompletedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), PRIMARY KEY(BusinessId,Id)
);

CREATE TABLE dbo.ItemSyncChanges (
 [Cursor] bigint IDENTITY NOT NULL PRIMARY KEY,
 BusinessId uniqueidentifier NOT NULL REFERENCES dbo.Businesses(Id),
 CollectionName varchar(32) NOT NULL, EntityId uniqueidentifier NOT NULL,
 Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1),
 CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_ItemSyncChanges_BusinessCursor ON dbo.ItemSyncChanges(BusinessId,[Cursor]);
CREATE INDEX IX_ItemSales_Report ON dbo.ItemSales(BusinessId,SaleDate) INCLUDE(TotalRevenueCentavos,TotalCostCentavos,TotalItems) WHERE DeletedAt IS NULL;
CREATE INDEX IX_BusinessExpenses_Report ON dbo.BusinessExpenses(BusinessId,ExpenseDate) INCLUDE(AmountCentavos,Category) WHERE DeletedAt IS NULL;
CREATE INDEX IX_ItemSaleLines_Item ON dbo.ItemSaleLines(BusinessId,ItemId) INCLUDE(SaleId,Quantity,LineRevenueCentavos,LineCostCentavos);
CREATE INDEX IX_StockMovements_Item ON dbo.StockMovements(BusinessId,ItemId,CreatedAt);
