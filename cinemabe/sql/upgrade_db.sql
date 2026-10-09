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
-- Staff security foundation: override PIN, audit log
-- ============================================================
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

-- ── P8a incidents ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.Incident', 'U') IS NULL
BEGIN
    CREATE TABLE [Incident] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NULL,
        [SeatId] uniqueidentifier NULL,
        [ShowTimeId] uniqueidentifier NULL,
        [Category] int NOT NULL,
        [Severity] int NOT NULL,
        [Status] int NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [ReportedByUserId] uniqueidentifier NOT NULL,
        [ResolvedByUserId] uniqueidentifier NULL,
        [ResolvedAt] datetime NULL,
        [ResolutionNote] nvarchar(1000) NULL,
        [BlocksSeat] bit NOT NULL DEFAULT 0,
        [BlocksRoom] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_Incident] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_Incident_TheaterId_Status_CreationTime] ON [Incident] ([TheaterId], [Status], [CreationTime]);
END
-- ── end P8a incidents ────────────────────────────────────────────────────────

-- ── P8b checklists ───────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.ChecklistTemplate', 'U') IS NULL
BEGIN
    CREATE TABLE [ChecklistTemplate] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Kind] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ChecklistTemplate] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ChecklistTemplate_TheaterId_Kind] ON [ChecklistTemplate] ([TheaterId], [Kind]) WHERE [IsActive] = 1;
END

IF OBJECT_ID('dbo.ChecklistTemplateItem', 'U') IS NULL
BEGIN
    CREATE TABLE [ChecklistTemplateItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [ChecklistTemplateId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Text] nvarchar(300) NOT NULL,
        [IsRequired] bit NOT NULL DEFAULT 1,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ChecklistTemplateItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChecklistTemplateItem_ChecklistTemplate] FOREIGN KEY ([ChecklistTemplateId]) REFERENCES [ChecklistTemplate] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ChecklistTemplateItem_ChecklistTemplateId] ON [ChecklistTemplateItem] ([ChecklistTemplateId]);
END

IF OBJECT_ID('dbo.ChecklistRun', 'U') IS NULL
BEGIN
    CREATE TABLE [ChecklistRun] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [ShowTimeId] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [Kind] int NOT NULL,
        [ChecklistTemplateId] uniqueidentifier NOT NULL,
        [TemplateName] nvarchar(200) NOT NULL,
        [CompletedAt] datetime NULL,
        [CompletedByUserId] uniqueidentifier NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ChecklistRun] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ChecklistRun_ShowTimeId_RoomId_Kind] ON [ChecklistRun] ([ShowTimeId], [RoomId], [Kind]);
    CREATE INDEX [IX_ChecklistRun_TheaterId_CreationTime] ON [ChecklistRun] ([TheaterId], [CreationTime]);
END

IF OBJECT_ID('dbo.ChecklistRunItem', 'U') IS NULL
BEGIN
    CREATE TABLE [ChecklistRunItem] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [ChecklistRunId] uniqueidentifier NOT NULL,
        [SortOrder] int NOT NULL,
        [Text] nvarchar(300) NOT NULL,
        [IsRequired] bit NOT NULL DEFAULT 1,
        [IsDone] bit NOT NULL DEFAULT 0,
        [DoneByUserId] uniqueidentifier NULL,
        [DoneAt] datetime NULL,
        [Note] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ChecklistRunItem] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChecklistRunItem_ChecklistRun] FOREIGN KEY ([ChecklistRunId]) REFERENCES [ChecklistRun] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ChecklistRunItem_ChecklistRunId] ON [ChecklistRunItem] ([ChecklistRunId]);
END
-- ── end P8b checklists ───────────────────────────────────────────────────────

-- ── P8c workforce ────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.StaffShift', 'U') IS NULL
BEGIN
    CREATE TABLE [StaffShift] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StartTime] datetime NOT NULL,
        [EndTime] datetime NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_StaffShift] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_StaffShift_TheaterId_StartTime] ON [StaffShift] ([TheaterId], [StartTime]);
    CREATE INDEX [IX_StaffShift_UserId_StartTime] ON [StaffShift] ([UserId], [StartTime]);
END

IF OBJECT_ID('dbo.TimeClockEntry', 'U') IS NULL
BEGIN
    CREATE TABLE [TimeClockEntry] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ClockInAt] datetime NOT NULL,
        [ClockOutAt] datetime NULL,
        [Note] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_TimeClockEntry] PRIMARY KEY ([Id])
    );
    -- At most one open (not clocked out) entry per user.
    CREATE UNIQUE INDEX [IX_TimeClockEntry_UserId_Open] ON [TimeClockEntry] ([UserId]) WHERE [ClockOutAt] IS NULL;
    CREATE INDEX [IX_TimeClockEntry_TheaterId_ClockInAt] ON [TimeClockEntry] ([TheaterId], [ClockInAt]);
END

IF OBJECT_ID('dbo.StaffTask', 'U') IS NULL
BEGIN
    CREATE TABLE [StaffTask] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [AssignedToUserId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [DueAt] datetime NULL,
        [Status] int NOT NULL,
        [CompletedAt] datetime NULL,
        [IncidentId] uniqueidentifier NULL,
        [ChecklistRunId] uniqueidentifier NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_StaffTask] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_StaffTask_AssignedToUserId_Status] ON [StaffTask] ([AssignedToUserId], [Status]);
    CREATE INDEX [IX_StaffTask_TheaterId_Status_CreationTime] ON [StaffTask] ([TheaterId], [Status], [CreationTime]);
END
-- ── end P8c workforce ────────────────────────────────────────────────────────

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
-- P4 box office: counter sales, split tenders, cash drawer
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). Statements that touch a column added in this
-- block run through EXEC so the batch compiles before the column exists.

-- Invoice: sale metadata; UserId becomes NULL so a walk-in counter sale needs no customer account.
IF COL_LENGTH('dbo.Invoice', 'TheaterId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [TheaterId] uniqueidentifier NULL;
END

IF COL_LENGTH('dbo.Invoice', 'Channel') IS NULL
BEGIN
    -- 0 = Online, 1 = Counter
    ALTER TABLE [Invoice] ADD [Channel] int NOT NULL CONSTRAINT [DF_Invoice_Channel] DEFAULT 0;
END

IF COL_LENGTH('dbo.Invoice', 'SoldByUserId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [SoldByUserId] uniqueidentifier NULL;
END

IF COL_LENGTH('dbo.Invoice', 'CashDrawerSessionId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [CashDrawerSessionId] uniqueidentifier NULL;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Invoice') AND name = 'UserId' AND is_nullable = 0)
BEGIN
    ALTER TABLE [Invoice] ALTER COLUMN [UserId] uniqueidentifier NULL;
END

-- Backfill the theater of existing (online) invoices from their tickets' room.
EXEC('UPDATE i SET i.[TheaterId] = x.[TheaterId]
      FROM [Invoice] i
      CROSS APPLY (SELECT TOP 1 r.[TheaterId] FROM [InvoiceTicket] t JOIN [Room] r ON r.[Id] = t.[RoomId] WHERE t.[InvoiceId] = i.[Id]) x
      WHERE i.[TheaterId] IS NULL');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Invoice_Theater_TheaterId')
BEGIN
    EXEC('ALTER TABLE [Invoice] ADD CONSTRAINT [FK_Invoice_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE NO ACTION');
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoice_TheaterId_PaidAt' AND object_id = OBJECT_ID('dbo.Invoice'))
BEGIN
    EXEC('CREATE INDEX [IX_Invoice_TheaterId_PaidAt] ON [Invoice] ([TheaterId], [PaidAt])');
END

-- One row per tender applied to an invoice (Method: 0 Cash, 1 Card, 2 QrWallet, 3 GiftCard, 4 Points, 5 Online).
IF OBJECT_ID('dbo.InvoicePayment', 'U') IS NULL
BEGIN
    CREATE TABLE [InvoicePayment] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [InvoiceId] uniqueidentifier NOT NULL,
        [Method] int NOT NULL,
        [Amount] float NOT NULL,
        [TenderedAmount] float NULL,
        [ChangeAmount] float NULL,
        [Reference] nvarchar(100) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_InvoicePayment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoicePayment_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_InvoicePayment_InvoiceId] ON [InvoicePayment] ([InvoiceId]);
END

-- A cashier's drawer shift (Status: 0 Open, 1 Closed, 2 Reconciled). One Open session per user and per terminal.
IF OBJECT_ID('dbo.CashDrawerSession', 'U') IS NULL
BEGIN
    CREATE TABLE [CashDrawerSession] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TerminalName] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [OpenedAt] datetime NOT NULL,
        [ClosedAt] datetime NULL,
        [OpeningFloat] float NOT NULL,
        [CountedCash] float NULL,
        [ExpectedCash] float NULL,
        [Variance] float NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_CashDrawerSession] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashDrawerSession_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashDrawerSession_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_CashDrawerSession_UserId] ON [CashDrawerSession] ([UserId]) WHERE [Status] = 0;
    CREATE UNIQUE INDEX [IX_CashDrawerSession_TheaterId_TerminalName] ON [CashDrawerSession] ([TheaterId], [TerminalName]) WHERE [Status] = 0;
END

-- Signed cash change in a session (Type: 0 OpeningFloat, 1 Sale, 2 Refund, 3 PayIn, 4 PayOut); expected cash = SUM(Amount).
IF OBJECT_ID('dbo.CashMovement', 'U') IS NULL
BEGIN
    CREATE TABLE [CashMovement] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [CashDrawerSessionId] uniqueidentifier NOT NULL,
        [TheaterId] uniqueidentifier NOT NULL,
        [Type] int NOT NULL,
        [Amount] float NOT NULL,
        [InvoiceId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_CashMovement] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashMovement_CashDrawerSession_CashDrawerSessionId] FOREIGN KEY ([CashDrawerSessionId]) REFERENCES [CashDrawerSession] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_CashMovement_CashDrawerSessionId] ON [CashMovement] ([CashDrawerSessionId]);
END
-- ===== end P4 box office =====

-- ============================================================
-- P6 food pickup: kitchen queue state on the invoice
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). FoodStatus: 0 None, 1 Pending, 2 Preparing, 3 Ready,
-- 4 HandedOver, 5 Cancelled. Statements that touch a column added in this block run through EXEC so the batch
-- compiles before the column exists.
IF COL_LENGTH('dbo.Invoice', 'FoodHandedOverAt') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [FoodHandedOverAt] datetime NULL;
END

IF COL_LENGTH('dbo.Invoice', 'FoodHandedOverByUserId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [FoodHandedOverByUserId] uniqueidentifier NULL;
END

IF COL_LENGTH('dbo.Invoice', 'FoodStatus') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [FoodStatus] int NOT NULL CONSTRAINT [DF_Invoice_FoodStatus] DEFAULT 0;

    -- Backfill (runs once, with the column): food already sold is history, so a paid invoice with food is HandedOver
    -- (stamped with its payment time), an unpaid one stays Pending, a cancelled/failed/refunded one is Cancelled.
    EXEC('UPDATE i SET i.[FoodStatus] = CASE i.[Status] WHEN 1 THEN 4 WHEN 0 THEN 1 ELSE 5 END,
                       i.[FoodHandedOverAt] = CASE WHEN i.[Status] = 1 THEN i.[PaidAt] ELSE NULL END
          FROM [Invoice] i
          WHERE EXISTS (SELECT 1 FROM [InvoiceFoodAndDrink] f WHERE f.[InvoiceId] = i.[Id])');
END
-- ===== end P6 food pickup =====

-- ============================================================
-- P5 after-sales: exchange link and refund reason on Invoice
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). The index runs through EXEC so the batch
-- compiles before the column exists.
IF COL_LENGTH('dbo.Invoice', 'ExchangedFromInvoiceId') IS NULL
BEGIN
    -- The invoice this one replaced in an exchange (no FK, like the other audit snapshots).
    ALTER TABLE [Invoice] ADD [ExchangedFromInvoiceId] uniqueidentifier NULL;
END

IF COL_LENGTH('dbo.Invoice', 'RefundReasonCode') IS NULL
BEGIN
    -- StaffReasonCode of a staff refund.
    ALTER TABLE [Invoice] ADD [RefundReasonCode] int NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoice_ExchangedFromInvoiceId' AND object_id = OBJECT_ID('dbo.Invoice'))
BEGIN
    EXEC('CREATE INDEX [IX_Invoice_ExchangedFromInvoiceId] ON [Invoice] ([ExchangedFromInvoiceId]) WHERE [ExchangedFromInvoiceId] IS NOT NULL');
END
-- ===== end P5 after-sales =====

-- ── P7 customer service ──────────────────────────────────────────────────────
-- Customer complaints and their compensation outcome. No FKs, like AuditLog/Incident: history survives deletions.
IF OBJECT_ID('dbo.Complaint', 'U') IS NULL
BEGIN
    CREATE TABLE [Complaint] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [CustomerUserId] uniqueidentifier NULL,
        [InvoiceId] uniqueidentifier NULL,
        [Category] int NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Status] int NOT NULL,
        [Resolution] int NOT NULL DEFAULT 0,
        [CompensationAmount] float NULL,
        [CompensationRef] nvarchar(100) NULL,
        [AssignedToUserId] uniqueidentifier NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [ResolvedByUserId] uniqueidentifier NULL,
        [ResolvedAt] datetime NULL,
        [ResolutionNote] nvarchar(1000) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_Complaint] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_Complaint_TheaterId_Status_CreationTime] ON [Complaint] ([TheaterId], [Status], [CreationTime]);
END
-- ── end P7 customer service ──────────────────────────────────────────────────

-- ============================================================
-- Removed staff roles cleanup: TheaterManager, BoxOfficeStaff, GateStaff, KitchenStaff, RegionalManager
-- ============================================================
IF EXISTS (
    SELECT 1 FROM [User] u
    JOIN [UserType] ut ON ut.Id = u.UserTypeId
    WHERE ut.[Name] IN (N'TheaterManager', N'BoxOfficeStaff', N'GateStaff', N'KitchenStaff', N'RegionalManager')
)
BEGIN
    THROW 50001, 'Users still hold a removed role (TheaterManager/BoxOfficeStaff/GateStaff/KitchenStaff/RegionalManager). Reassign them to another role before re-running this script.', 1;
END

IF OBJECT_ID('dbo.UserTheater', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.UserTheater;
END

DELETE FROM [UserType] WHERE [Name] IN (N'TheaterManager', N'BoxOfficeStaff', N'GateStaff', N'KitchenStaff', N'RegionalManager');

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
