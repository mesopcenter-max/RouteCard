IF DB_ID(N'RouteCard') IS NULL
BEGIN
	CREATE DATABASE RouteCard;
END;
GO

USE RouteCard;
GO

IF OBJECT_ID(N'dbo.RouteCards', N'U') IS NULL
BEGIN
	CREATE TABLE dbo.RouteCards
	(
		Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RouteCards PRIMARY KEY,
		RouteCardNo NVARCHAR(100) NULL,
		OrderNo NVARCHAR(100) NULL,
		Qty DECIMAL(18,3) NULL,
		WorkOrderNo NVARCHAR(100) NULL,
		JcQty DECIMAL(18,3) NULL,
		PartNo NVARCHAR(100) NOT NULL,
		PartName NVARCHAR(250) NULL,
		PslNo NVARCHAR(100) NULL,
		HalDrawingNo NVARCHAR(250) NULL,
		IssueNo NVARCHAR(100) NULL,
		PartsPerUnit NVARCHAR(100) NULL,
		ReferenceDrawingNo NVARCHAR(250) NULL,
		MaterialSpecification NVARCHAR(250) NULL,
		MaterialIssue NVARCHAR(100) NULL,
		MaterialDescription NVARCHAR(500) NULL,
		MrNo NVARCHAR(100) NULL,
		MaterialDate DATETIME2 NULL,
		MaterialType NVARCHAR(100) NULL,
		ReceivedCondition NVARCHAR(250) NULL,
		RawMaterialDia NVARCHAR(100) NULL,
		RawMaterialLength NVARCHAR(100) NULL,
		RawMaterialBatchNo NVARCHAR(100) NULL,
		AlternativeMaterialSize NVARCHAR(250) NULL,
		RubberSpecification NVARCHAR(250) NULL,
		RubberMixDate DATETIME2 NULL,
		RubberExpiryDate DATETIME2 NULL,
		RubberQty DECIMAL(18,3) NULL,
		RubberSupplier NVARCHAR(250) NULL,
		TaPcRef NVARCHAR(250) NULL,
		BsWeight NVARCHAR(100) NULL,
		ForgingWeight NVARCHAR(100) NULL,
		ForgingPlant NVARCHAR(100) NULL,
		ForgingBatchNo NVARCHAR(100) NULL,
		ForgingQty DECIMAL(18,3) NULL,
		RubberBatchNo NVARCHAR(100) NULL,
		RubberLtrNo NVARCHAR(100) NULL,
		UnFett NVARCHAR(100) NULL,
		FettWeight NVARCHAR(100) NULL,
		CastingBatchNo NVARCHAR(100) NULL,
		CastingQty DECIMAL(18,3) NULL,
		PadWeight NVARCHAR(100) NULL,
		PlatingBatchNo NVARCHAR(100) NULL,
		MixBatchNo NVARCHAR(100) NULL,
		PowderQty DECIMAL(18,3) NULL,
		TraceabilityJson NVARCHAR(MAX) NULL,
		OperationsJson NVARCHAR(MAX) NULL,
		CreatedUtc DATETIME2 NOT NULL CONSTRAINT DF_RouteCards_CreatedUtc DEFAULT SYSUTCDATETIME(),
		UpdatedUtc DATETIME2 NOT NULL CONSTRAINT DF_RouteCards_UpdatedUtc DEFAULT SYSUTCDATETIME()
	);
	CREATE INDEX IX_RouteCards_PartNo ON dbo.RouteCards(PartNo);
	CREATE INDEX IX_RouteCards_OrderNo ON dbo.RouteCards(OrderNo);
	CREATE INDEX IX_RouteCards_WorkOrderNo ON dbo.RouteCards(WorkOrderNo);
END;
GO

IF COL_LENGTH(N'dbo.RouteCards', N'CreatedByUserId') IS NULL
    ALTER TABLE dbo.RouteCards ADD CreatedByUserId INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RouteCards_AppUsers_CreatedByUserId')
    ALTER TABLE dbo.RouteCards ADD CONSTRAINT FK_RouteCards_AppUsers_CreatedByUserId FOREIGN KEY (CreatedByUserId) REFERENCES dbo.AppUsers(Id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RouteCards_CreatedByUserId' AND object_id = OBJECT_ID(N'dbo.RouteCards'))
    CREATE INDEX IX_RouteCards_CreatedByUserId ON dbo.RouteCards(CreatedByUserId);
GO

IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        DisplayName NVARCHAR(100) NOT NULL,
        Username NVARCHAR(200) NOT NULL CONSTRAINT UQ_AppUsers_Username UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        PhotoUrl NVARCHAR(500) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.UserStamps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserStamps
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserStamps PRIMARY KEY,
        AppUserId INT NOT NULL CONSTRAINT FK_UserStamps_AppUsers REFERENCES dbo.AppUsers(Id),
        StampValue NVARCHAR(200) NOT NULL,
        SignatureValue NVARCHAR(200) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.RouteCardApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteCardApprovals
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RouteCardApprovals PRIMARY KEY,
        RouteCardId BIGINT NOT NULL CONSTRAINT FK_RouteCardApprovals_RouteCards REFERENCES dbo.RouteCards(Id),
        AppUserId INT NOT NULL CONSTRAINT FK_RouteCardApprovals_AppUsers REFERENCES dbo.AppUsers(Id),
        UserStampId INT NOT NULL CONSTRAINT FK_RouteCardApprovals_UserStamps REFERENCES dbo.UserStamps(Id),
        OperationNo NVARCHAR(100) NOT NULL,
        StampValue NVARCHAR(200) NOT NULL,
        SignatureValue NVARCHAR(200) NOT NULL,
        ApprovedAt DATETIME2 NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.ProcessRecordAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcessRecordAttachments
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProcessRecordAttachments PRIMARY KEY,
        RouteCardId BIGINT NOT NULL CONSTRAINT FK_ProcessRecordAttachments_RouteCards REFERENCES dbo.RouteCards(Id),
        OperationNo NVARCHAR(100) NOT NULL,
        DepartmentId NVARCHAR(100) NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        Content VARBINARY(MAX) NOT NULL,
        AttachedByUserId INT NOT NULL CONSTRAINT FK_ProcessRecordAttachments_AppUsers REFERENCES dbo.AppUsers(Id),
        AttachedByName NVARCHAR(100) NOT NULL,
        AttachedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ProcessRecordAttachments_AttachedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ProcessRecordAttachments_OperationNo ON dbo.ProcessRecordAttachments(OperationNo);
    CREATE INDEX IX_ProcessRecordAttachments_DepartmentId ON dbo.ProcessRecordAttachments(DepartmentId);
    CREATE INDEX IX_ProcessRecordAttachments_AttachedAtUtc ON dbo.ProcessRecordAttachments(AttachedAtUtc);
END;
GO
