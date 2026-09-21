-- Forward-only upgrade for databases created before multi-business accounts.
-- The guards also make this a no-op for databases created from the consolidated baseline.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Businesses', N'U') IS NULL
    THROW 50000, 'Apply the initial schema before migration 003.', 1;
GO

IF COL_LENGTH(N'dbo.Users', N'PhoneNumber') IS NULL
    ALTER TABLE dbo.Users ADD PhoneNumber varchar(13) NULL;
GO

IF COL_LENGTH(N'dbo.Users', N'EmailVerified') IS NULL
    ALTER TABLE dbo.Users ADD EmailVerified bit NOT NULL
        CONSTRAINT DF_Users_EmailVerified DEFAULT 0;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Users') AND name = N'CK_Users_Phone')
BEGIN
    ALTER TABLE dbo.Users ADD CONSTRAINT CK_Users_Phone CHECK (
        PhoneNumber IS NULL OR
        (LEN(PhoneNumber) = 13 AND PhoneNumber LIKE '+639%'
         AND SUBSTRING(PhoneNumber, 5, 9) NOT LIKE '%[^0-9]%' COLLATE Latin1_General_100_BIN2));
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'UX_Users_PhoneNumber')
    CREATE UNIQUE INDEX UX_Users_PhoneNumber
        ON dbo.Users(PhoneNumber) WHERE PhoneNumber IS NOT NULL;
GO

-- The original unnamed UNIQUE constraint enforced one business per owner.
DECLARE @ownerConstraint sysname;
DECLARE @dropOwnerConstraint nvarchar(max);
SELECT TOP (1) @ownerConstraint = kc.name
FROM sys.key_constraints kc
JOIN sys.index_columns ic
  ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c
  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Businesses')
  AND kc.type = 'UQ'
  AND c.name = N'OwnerUid';

IF @ownerConstraint IS NOT NULL
BEGIN
    SET @dropOwnerConstraint =
        N'ALTER TABLE dbo.Businesses DROP CONSTRAINT ' + QUOTENAME(@ownerConstraint);
    EXEC sys.sp_executesql @dropOwnerConstraint;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Businesses') AND name = N'IX_Businesses_Owner')
    CREATE INDEX IX_Businesses_Owner ON dbo.Businesses(OwnerUid);
GO

IF OBJECT_ID(N'dbo.BusinessRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessRequests (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        OwnerUid uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
        Name nvarchar(160) NOT NULL,
        DefaultLocation nvarchar(500) NOT NULL,
        Status varchar(20) NOT NULL
            CONSTRAINT CK_BusinessRequests_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
        Reason nvarchar(1000) NULL,
        BusinessId uniqueidentifier NULL REFERENCES dbo.Businesses(Id),
        CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
        DecidedAt datetimeoffset NULL,
        DecidedBy uniqueidentifier NULL REFERENCES dbo.Users(Id),
        CONSTRAINT CK_BusinessRequests_Decision CHECK (
            (Status = 'Pending' AND DecidedAt IS NULL AND DecidedBy IS NULL AND BusinessId IS NULL) OR
            (Status = 'Approved' AND DecidedAt IS NOT NULL AND DecidedBy IS NOT NULL AND BusinessId IS NOT NULL) OR
            (Status = 'Rejected' AND DecidedAt IS NOT NULL AND DecidedBy IS NOT NULL
             AND BusinessId IS NULL AND Reason IS NOT NULL AND LEN(LTRIM(RTRIM(Reason))) > 0))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BusinessRequests') AND name = N'UX_BusinessRequests_PendingOwner')
    CREATE UNIQUE INDEX UX_BusinessRequests_PendingOwner
        ON dbo.BusinessRequests(OwnerUid) WHERE Status = 'Pending';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BusinessRequests') AND name = N'IX_BusinessRequests_Owner')
    CREATE INDEX IX_BusinessRequests_Owner ON dbo.BusinessRequests(OwnerUid, CreatedAt);
GO

IF OBJECT_ID(N'dbo.AdminAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminAudit (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        ActorUid uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
        Action varchar(40) NOT NULL,
        SubjectUid uniqueidentifier NOT NULL REFERENCES dbo.Users(Id),
        RecipientUid uniqueidentifier NULL REFERENCES dbo.Users(Id),
        RequestId uniqueidentifier NULL REFERENCES dbo.BusinessRequests(Id),
        PhoneNumber varchar(13) NULL,
        PreviousRecipientPhone varchar(13) NULL,
        Reason nvarchar(1000) NOT NULL,
        CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO
