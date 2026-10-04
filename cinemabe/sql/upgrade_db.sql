-- ============================================================
-- upgrade_db.sql  —  Schema upgrade script
-- Run this on an existing Cinema database when updating from a database
-- predating the current create_db.sql baseline.
--
-- Reset alongside a full rewrite of create_db.sql (new baseline, new database) —
-- there is nothing to apply from the old baseline to the new one. Add new
-- ALTER TABLE / CREATE TABLE statements below as the schema evolves from here.
-- Each change should be guarded with IF NOT EXISTS checks so the script stays
-- safely re-runnable.
-- ============================================================

-- ============================================================
-- Inventory: opt-in stock tracking, combos, stock ledger, storage plans
-- ============================================================
-- Existing rows end up untracked (TrackInventory = 0) and non-combo, so behaviour is unchanged until
-- an admin switches tracking on.

IF COL_LENGTH('dbo.FoodAndDrink', 'TrackInventory') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [TrackInventory] bit NOT NULL CONSTRAINT [DF_FoodAndDrink_TrackInventory] DEFAULT 0;
END

IF COL_LENGTH('dbo.FoodAndDrink', 'QuantityOnHand') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [QuantityOnHand] int NOT NULL CONSTRAINT [DF_FoodAndDrink_QuantityOnHand] DEFAULT 0;
END

IF COL_LENGTH('dbo.FoodAndDrink', 'LowStockThreshold') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [LowStockThreshold] int NOT NULL CONSTRAINT [DF_FoodAndDrink_LowStockThreshold] DEFAULT 0;
END

IF COL_LENGTH('dbo.FoodAndDrink', 'TargetStockLevel') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [TargetStockLevel] int NOT NULL CONSTRAINT [DF_FoodAndDrink_TargetStockLevel] DEFAULT 0;
END

IF COL_LENGTH('dbo.FoodAndDrink', 'IsCombo') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [IsCombo] bit NOT NULL CONSTRAINT [DF_FoodAndDrink_IsCombo] DEFAULT 0;
END

IF OBJECT_ID('dbo.CK_FoodAndDrink_QuantityOnHand', 'C') IS NULL
BEGIN
    EXEC('ALTER TABLE [FoodAndDrink] ADD CONSTRAINT [CK_FoodAndDrink_QuantityOnHand] CHECK ([QuantityOnHand] >= 0)');
END

IF OBJECT_ID('dbo.ComboItem', 'U') IS NULL
BEGIN
    CREATE TABLE [ComboItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [ComboId] uniqueidentifier NOT NULL,
        [ComponentId] uniqueidentifier NOT NULL,
        [Quantity] int NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ComboItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComboItem_FoodAndDrink_ComboId] FOREIGN KEY ([ComboId]) REFERENCES [FoodAndDrink] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComboItem_FoodAndDrink_ComponentId] FOREIGN KEY ([ComponentId]) REFERENCES [FoodAndDrink] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_ComboItem_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_ComboItem_NotSelf] CHECK ([ComboId] <> [ComponentId])
    );
    CREATE UNIQUE INDEX [IX_ComboItem_ComboId_ComponentId] ON [ComboItem] ([ComboId], [ComponentId]);
    CREATE INDEX [IX_ComboItem_ComponentId] ON [ComboItem] ([ComponentId]);
END

IF OBJECT_ID('dbo.StockMovement', 'U') IS NULL
BEGIN
    CREATE TABLE [StockMovement] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [FoodAndDrinkId] uniqueidentifier NOT NULL,
        [TheaterId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Quantity] int NOT NULL,
        [ReasonCode] int NULL,
        [Reason] nvarchar(500) NULL,
        [InvoiceId] uniqueidentifier NULL,
        [StoragePlanId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_StockMovement] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockMovement_FoodAndDrink_FoodAndDrinkId] FOREIGN KEY ([FoodAndDrinkId]) REFERENCES [FoodAndDrink] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_StockMovement_FoodAndDrinkId_CreationTime] ON [StockMovement] ([FoodAndDrinkId], [CreationTime]);
    CREATE INDEX [IX_StockMovement_TheaterId_CreationTime] ON [StockMovement] ([TheaterId], [CreationTime]);
    CREATE INDEX [IX_StockMovement_InvoiceId] ON [StockMovement] ([InvoiceId]);
END

IF OBJECT_ID('dbo.StoragePlan', 'U') IS NULL
BEGIN
    CREATE TABLE [StoragePlan] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Code] nvarchar(30) NOT NULL,
        [TheaterId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [TargetDate] datetime NOT NULL,
        [Supplier] nvarchar(200) NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [SubmittedAt] datetime NULL,
        [DecidedByUserId] uniqueidentifier NULL,
        [DecidedAt] datetime NULL,
        [RejectionReason] nvarchar(500) NULL,
        [ReceivedByUserId] uniqueidentifier NULL,
        [ReceivedAt] datetime NULL,
        [RowVersion] rowversion NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_StoragePlan] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoragePlan_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_StoragePlan_Code] ON [StoragePlan] ([Code]);
    CREATE INDEX [IX_StoragePlan_TheaterId_Status] ON [StoragePlan] ([TheaterId], [Status]);
END

IF OBJECT_ID('dbo.StoragePlanItem', 'U') IS NULL
BEGIN
    CREATE TABLE [StoragePlanItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [StoragePlanId] uniqueidentifier NOT NULL,
        [FoodAndDrinkId] uniqueidentifier NOT NULL,
        [PlannedQuantity] int NOT NULL,
        [ReceivedQuantity] int NULL,
        [UnitCost] float NULL,
        [Note] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_StoragePlanItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StoragePlanItem_StoragePlan_StoragePlanId] FOREIGN KEY ([StoragePlanId]) REFERENCES [StoragePlan] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StoragePlanItem_FoodAndDrink_FoodAndDrinkId] FOREIGN KEY ([FoodAndDrinkId]) REFERENCES [FoodAndDrink] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [CK_StoragePlanItem_PlannedQuantity] CHECK ([PlannedQuantity] > 0)
    );
    CREATE UNIQUE INDEX [IX_StoragePlanItem_StoragePlanId_FoodAndDrinkId] ON [StoragePlanItem] ([StoragePlanId], [FoodAndDrinkId]);
    CREATE INDEX [IX_StoragePlanItem_FoodAndDrinkId] ON [StoragePlanItem] ([FoodAndDrinkId]);
END

-- ============================================================
-- Staff roles: TheaterManager user type
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'TheaterManager')
BEGIN
    INSERT INTO [UserType] ([Id], [Name], [CreationTime])
    VALUES (NEWID(), N'TheaterManager', GETUTCDATE());
END

-- ============================================================
-- Staff security foundation: roles, override PIN, audit log
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'BoxOfficeStaff')
BEGIN
    INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES (NEWID(), N'BoxOfficeStaff', GETUTCDATE());
END

IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'GateStaff')
BEGIN
    INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES (NEWID(), N'GateStaff', GETUTCDATE());
END

IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'KitchenStaff')
BEGIN
    INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES (NEWID(), N'KitchenStaff', GETUTCDATE());
END

IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'RegionalManager')
BEGIN
    INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES (NEWID(), N'RegionalManager', GETUTCDATE());
END

IF COL_LENGTH('dbo.User', 'OverridePinHash') IS NULL
BEGIN
    ALTER TABLE [User] ADD [OverridePinHash] varbinary(64) NULL;
END

IF COL_LENGTH('dbo.User', 'OverridePinSalt') IS NULL
BEGIN
    ALTER TABLE [User] ADD [OverridePinSalt] varbinary(128) NULL;
END

IF COL_LENGTH('dbo.User', 'OverridePinFailedCount') IS NULL
BEGIN
    ALTER TABLE [User] ADD [OverridePinFailedCount] int NOT NULL CONSTRAINT [DF_User_OverridePinFailedCount] DEFAULT 0;
END

IF COL_LENGTH('dbo.User', 'OverridePinLockoutEndUtc') IS NULL
BEGIN
    ALTER TABLE [User] ADD [OverridePinLockoutEndUtc] datetime NULL;
END

IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
BEGIN
    CREATE TABLE [AuditLog] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [ApproverUserId] uniqueidentifier NULL,
        [Action] int NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [Amount] float NULL,
        [ReasonCode] int NULL,
        [Reason] nvarchar(500) NULL,
        [DataJson] nvarchar(max) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_AuditLog] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AuditLog_TheaterId_CreationTime] ON [AuditLog] ([TheaterId], [CreationTime]);
    CREATE INDEX [IX_AuditLog_ActorUserId_CreationTime] ON [AuditLog] ([ActorUserId], [CreationTime]);
END

-- ============================================================
-- P3 gate: InvoiceTicket.UsedAt / UsedByUserId
-- ============================================================
IF COL_LENGTH('dbo.InvoiceTicket', 'UsedAt') IS NULL
BEGIN
    ALTER TABLE [InvoiceTicket] ADD [UsedAt] datetime NULL;
END
IF COL_LENGTH('dbo.InvoiceTicket', 'UsedByUserId') IS NULL
BEGIN
    ALTER TABLE [InvoiceTicket] ADD [UsedByUserId] uniqueidentifier NULL;
END

-- ============================================================
-- EF Core migrations baseline
-- ============================================================
-- Stamping the history table here marks a database upgraded via this script as already
-- migrated, so `dotnet ef database update` applies only later migrations instead of trying
-- to re-create everything. Keep this row in sync with create_db.sql's baseline stamp.

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260726064153_InitialBaseline')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726064153_InitialBaseline', N'9.0.0');
END
