ALTER TABLE dbo.Products
ADD RecipeBatchYield decimal(19,6) NOT NULL
    CONSTRAINT DF_Products_RecipeBatchYield DEFAULT (1);
GO

ALTER TABLE dbo.Products
ADD CONSTRAINT CK_Products_RecipeBatchYield CHECK (RecipeBatchYield > 0);
