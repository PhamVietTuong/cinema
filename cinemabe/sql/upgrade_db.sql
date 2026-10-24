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
-- P5 after-sales: exchange link on Invoice
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). The index runs through EXEC so the batch
-- compiles before the column exists.
IF COL_LENGTH('dbo.Invoice', 'ExchangedFromInvoiceId') IS NULL
BEGIN
    -- The invoice this one replaced in an exchange (no FK, like the other audit snapshots).
    ALTER TABLE [Invoice] ADD [ExchangedFromInvoiceId] uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoice_ExchangedFromInvoiceId' AND object_id = OBJECT_ID('dbo.Invoice'))
BEGIN
    EXEC('CREATE INDEX [IX_Invoice_ExchangedFromInvoiceId] ON [Invoice] ([ExchangedFromInvoiceId]) WHERE [ExchangedFromInvoiceId] IS NOT NULL');
END
-- ===== end P5 after-sales =====

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
-- Weekly ticket pricing (TicketPrice.DayOfWeek)
-- ============================================================
-- Replaces the old IsHoliday bit with a System.DayOfWeek column (0=Sunday..6=Saturday), so a row can
-- price Mon-Sun independently. Holiday pricing moves to the Holiday table, matched by calendar date,
-- so former IsHoliday=1 rows are dropped and each remaining row fans out to all 7 days with the same
-- multiplier it already had. Re-running this block is a no-op.
IF COL_LENGTH('dbo.TicketPrice', 'IsHoliday') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRAN;

    -- Drop every index that references IsHoliday, not just the one name this baseline expects —
    -- a database that picked up a differently-named index on that column (e.g. from an older,
    -- unmigrated baseline) would otherwise fail at DROP COLUMN below instead of here.
    DECLARE @dropIndexSql nvarchar(max) = (
        SELECT STRING_AGG(CAST('DROP INDEX [' + i.name + '] ON [TicketPrice];' AS nvarchar(max)), ' ')
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID('dbo.TicketPrice') AND c.name = 'IsHoliday'
    );
    IF @dropIndexSql IS NOT NULL
    BEGIN
        EXEC(@dropIndexSql);
    END

    IF COL_LENGTH('dbo.TicketPrice', 'DayOfWeek') IS NULL
    BEGIN
        ALTER TABLE [TicketPrice] ADD [DayOfWeek] int NULL;
    END

    EXEC('DELETE FROM [TicketPrice] WHERE [IsHoliday] = 1');

    EXEC('ALTER TABLE [TicketPrice] DROP COLUMN [IsHoliday]');

    EXEC('UPDATE [TicketPrice] SET [DayOfWeek] = 0');

    EXEC('INSERT INTO [TicketPrice] ([Id], [TheaterId], [RoomTypeId], [TimeSlotId], [DayOfWeek], [PriceMultiplier], [CreationTime])
          SELECT NEWID(), tp.[TheaterId], tp.[RoomTypeId], tp.[TimeSlotId], d.[D], tp.[PriceMultiplier], GETUTCDATE()
          FROM [TicketPrice] tp
          CROSS JOIN (VALUES (1), (2), (3), (4), (5), (6)) AS d(D)
          WHERE tp.[DayOfWeek] = 0');

    EXEC('ALTER TABLE [TicketPrice] ALTER COLUMN [DayOfWeek] int NOT NULL');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketPrice_TheaterId_RoomTypeId_TimeSlotId_DayOfWeek' AND object_id = OBJECT_ID('dbo.TicketPrice'))
    BEGIN
        EXEC('CREATE UNIQUE INDEX [IX_TicketPrice_TheaterId_RoomTypeId_TimeSlotId_DayOfWeek] ON [TicketPrice] ([TheaterId], [RoomTypeId], [TimeSlotId], [DayOfWeek])');
    END

    COMMIT TRAN;
END

-- ============================================================
-- InvoiceTicket.SeatLabel: seat label frozen at sale time
-- ============================================================
-- InvoiceTicket has a filtered index (IX_InvoiceTicket_ActiveSeat), so any UPDATE against it requires
-- these session settings, matching the defaults EF Core's own migrations always emit.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF COL_LENGTH('dbo.InvoiceTicket', 'SeatLabel') IS NULL
BEGIN
    ALTER TABLE [InvoiceTicket] ADD [SeatLabel] nvarchar(16) NULL;
END

-- One-time BEST-EFFORT backfill: numbers each existing ticket's seat with TODAY's room layout
-- (row-position numbering; a double seat = one slot; blocked seats still count). If the room was
-- re-paired/resized since the sale this is NOT a true historical reconstruction — it only keeps old
-- rows from being blank. Must stay equivalent to Cinema.Business/Helpers/SeatNumbering.cs. Safe to
-- re-run: only rows where SeatLabel IS NULL are touched.
EXEC('
;WITH s1 AS (
    SELECT s.[Id], s.[RoomId], s.[RowName], s.[ColIndex], s.[SeatGroupId],
           LAG(s.[SeatGroupId]) OVER (PARTITION BY s.[RoomId], s.[RowName] ORDER BY s.[ColIndex]) AS [PrevGroupId],
           COUNT(*) OVER (PARTITION BY s.[RoomId], s.[RowName], s.[SeatGroupId]) AS [GroupSizeInRow]
    FROM [Seat] s
), s2 AS (
    SELECT [Id], [RowName],
           SUM(CASE WHEN [SeatGroupId] IS NOT NULL AND [PrevGroupId] = [SeatGroupId] AND [GroupSizeInRow] = 2
                    THEN 0 ELSE 1 END)
               OVER (PARTITION BY [RoomId], [RowName] ORDER BY [ColIndex] ROWS UNBOUNDED PRECEDING) AS [DisplayNumber]
    FROM s1
)
UPDATE t SET t.[SeatLabel] = s2.[RowName] + CAST(s2.[DisplayNumber] AS nvarchar(10))
FROM [InvoiceTicket] t
JOIN s2 ON s2.[Id] = t.[SeatId]
WHERE t.[SeatLabel] IS NULL');

-- ============================================================
-- Removed staff features cleanup (after-sales, cash close, drawer, complaints, time clock, tasks, storage plans, refund, checklists)
-- ============================================================
-- No role check like the "removed staff roles" block above: these tables just stop being used by the
-- app (EF no longer maps them), so dropping them is safe even before this script runs. FK-safe order:
-- children before parents.
IF OBJECT_ID('dbo.CashMovement', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.CashMovement;
END

IF OBJECT_ID('dbo.CashDrawerSession', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.CashDrawerSession;
END

IF OBJECT_ID('dbo.StoragePlanItem', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.StoragePlanItem;
END

IF OBJECT_ID('dbo.StoragePlan', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.StoragePlan;
END

IF OBJECT_ID('dbo.StaffTask', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.StaffTask;
END

IF OBJECT_ID('dbo.TimeClockEntry', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.TimeClockEntry;
END

IF OBJECT_ID('dbo.Complaint', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Complaint;
END

-- Refund feature removed: old Refunded (4) invoices already had their seats/points/stock reversed, so
-- they are reclassified as Cancelled before the now-unused columns are dropped.
IF COL_LENGTH('dbo.Invoice', 'Status') IS NOT NULL
BEGIN
    UPDATE [Invoice] SET [Status] = 2 WHERE [Status] = 4;
END

IF COL_LENGTH('dbo.Invoice', 'RefundedAt') IS NOT NULL
BEGIN
    ALTER TABLE [Invoice] DROP COLUMN [RefundedAt];
END

IF COL_LENGTH('dbo.Invoice', 'RefundReasonCode') IS NOT NULL
BEGIN
    ALTER TABLE [Invoice] DROP COLUMN [RefundReasonCode];
END

IF OBJECT_ID('dbo.ChecklistRunItem', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChecklistRunItem;
END

IF OBJECT_ID('dbo.ChecklistRun', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChecklistRun;
END

IF OBJECT_ID('dbo.ChecklistTemplateItem', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChecklistTemplateItem;
END

IF OBJECT_ID('dbo.ChecklistTemplate', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChecklistTemplate;
END

-- ============================================================
-- Loyalty points: admin-configurable conversion rate (global default + per-theater override)
-- ============================================================
IF OBJECT_ID('dbo.LoyaltySetting', 'U') IS NULL
BEGIN
    CREATE TABLE [LoyaltySetting] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NULL,
        [PointsPerUnit] int NOT NULL,
        [PointValueVnd] int NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_LoyaltySetting] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_LoyaltySetting_TheaterId] ON [LoyaltySetting] ([TheaterId]) WHERE [TheaterId] IS NOT NULL;
END

IF NOT EXISTS (SELECT 1 FROM [LoyaltySetting] WHERE [TheaterId] IS NULL)
BEGIN
    INSERT INTO [LoyaltySetting] ([Id], [TheaterId], [PointsPerUnit], [PointValueVnd], [CreationTime]) VALUES
    (NEWID(), NULL, 1000, 1000, GETUTCDATE());
END

-- ===== patron category ID-check notice =====
IF COL_LENGTH('dbo.PatronCategory', 'RequiresIdCheck') IS NULL
BEGIN
    ALTER TABLE [PatronCategory] ADD [RequiresIdCheck] bit NOT NULL CONSTRAINT [DF_PatronCategory_RequiresIdCheck] DEFAULT 0;
END

IF COL_LENGTH('dbo.PatronCategory', 'IdCheckMessage') IS NULL
BEGIN
    ALTER TABLE [PatronCategory] ADD [IdCheckMessage] nvarchar(1000) NULL;
END
-- ===== end patron category ID-check notice =====

-- ============================================================
-- Notification templates: DB-stored email/SMS templates ({{Token}} placeholders)
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). Code is the lookup key the sending
-- code (BookingManager) uses; Channel: 1 Email, 2 Sms.
IF OBJECT_ID('dbo.Template', 'U') IS NULL
BEGIN
    CREATE TABLE [Template] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Channel] int NOT NULL,
        [Subject] nvarchar(500) NULL,
        [Body] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_Template] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Template_Code] ON [Template] ([Code]);
END

IF NOT EXISTS (SELECT 1 FROM [Template] WHERE [Code] = N'BookingConfirmationEmail')
BEGIN
    INSERT INTO [Template] ([Id], [Code], [Name], [Channel], [Subject], [Body], [IsActive], [CreationTime]) VALUES
    (NEWID(), N'BookingConfirmationEmail', N'Xác nhận đặt vé (Email)', 1, N'Đặt vé thành công — {{BookingCode}}',
N'<div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;background:#faf6f0;padding:24px;">
  <div style="text-align:center;margin-bottom:16px;">
    <h2 style="color:#2b2420;margin:8px 0 4px;">Đặt Vé Thành Công!</h2>
    <p style="color:#6b6258;margin:0;">Cảm ơn bạn, {{CustomerName}}. Vé của bạn đã được xác nhận.</p>
  </div>
  <div style="background:#ffffff;border-radius:12px;padding:20px;border:1px solid #eee2d2;">
    <p><strong>Mã đặt vé:</strong> {{BookingCode}}</p>
    <p><strong>Phim:</strong> {{MovieTitle}}</p>
    <p><strong>Rạp chiếu:</strong> {{TheaterName}}</p>
    <p><strong>Suất chiếu:</strong> {{ShowTime}}</p>
    <p><strong>Phòng chiếu:</strong> {{RoomName}} ({{RoomTypeName}}, {{ProjectionForm}})</p>
    <p><strong>Loại vé:</strong> {{TicketTypes}}</p>
    <p><strong>Ghế:</strong> {{SeatLabels}}</p>
    <p><strong>Bắp nước:</strong> {{Concessions}}</p>
    <p style="font-size:18px;"><strong>Tổng tiền:</strong> {{TotalAmount}}</p>
    <div style="text-align:center;margin-top:12px;">{{QrImages}}</div>
    <p style="text-align:center;color:#9a9186;font-size:12px;">Mã vé: {{QrCodes}}</p>
  </div>
  <p style="text-align:center;color:#9a9186;margin-top:16px;">Xuất trình mã QR tại quầy hoặc cổng soát vé.</p>
</div>', 1, GETUTCDATE());
END

-- Existing DBs seeded before {{QrImages}} existed: rewrite only a still-stock body, leave admin customizations alone.
UPDATE [Template]
SET [Body] = REPLACE([Body], N'<p><strong>Mã vé (QR):</strong> {{QrCodes}}</p>', N'<div style="text-align:center;margin-top:12px;">{{QrImages}}</div>
    <p style="text-align:center;color:#9a9186;font-size:12px;">Mã vé: {{QrCodes}}</p>'),
    [LastUpdatedTime] = GETUTCDATE()
WHERE [Code] = N'BookingConfirmationEmail'
  AND [Body] NOT LIKE N'%{{QrImages}}%'
  AND [Body] LIKE N'%<p><strong>Mã vé (QR):</strong> {{QrCodes}}</p>%';

IF NOT EXISTS (SELECT 1 FROM [Template] WHERE [Code] = N'BookingConfirmationSms')
BEGIN
    INSERT INTO [Template] ([Id], [Code], [Name], [Channel], [Subject], [Body], [IsActive], [CreationTime]) VALUES
    (NEWID(), N'BookingConfirmationSms', N'Xác nhận đặt vé (SMS)', 2, NULL,
N'Cinema: dat ve {{BookingCode}} thanh cong. {{MovieTitle}} {{ShowTime}} {{RoomName}}, ghe {{SeatLabels}}. Tong {{TotalAmount}}. Xuat trinh ma QR tai cong soat ve.', 1, GETUTCDATE());
END
-- ===== end notification templates =====

-- ============================================================
-- Promotion programs: customer-facing promo cards (Cinestar-style), separate from Discount.
-- Identical in create_db.sql and upgrade_db.sql (idempotent).
-- ============================================================
IF OBJECT_ID('dbo.PromotionProgram', 'U') IS NULL
BEGIN
    CREATE TABLE [PromotionProgram] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Title] nvarchar(200) NOT NULL,
        [Summary] nvarchar(500) NULL,
        [BannerUrl] nvarchar(max) NULL,
        [Mechanic] int NOT NULL DEFAULT 0,
        [PercentOff] float NULL,
        [Eligibility] int NOT NULL DEFAULT 0,
        [MinMemberShipId] uniqueidentifier NULL,
        [MaxAge] int NULL,
        [StartDate] datetime NOT NULL,
        [EndDate] datetime NOT NULL,
        [ExcludeHolidays] bit NOT NULL DEFAULT 0,
        [AllowedChannelsMask] int NULL,
        [MaxTicketsPerTransaction] int NULL,
        [StacksWithMembershipDiscount] bit NOT NULL DEFAULT 0,
        [TermsText] nvarchar(2000) NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [IsPublished] bit NOT NULL DEFAULT 0,
        [AutoApply] bit NOT NULL DEFAULT 0,
        [LinkedDiscountId] uniqueidentifier NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_PromotionProgram] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PromotionProgram_MemberShip_MinMemberShipId] FOREIGN KEY ([MinMemberShipId]) REFERENCES [MemberShip] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionProgram_Discount_LinkedDiscountId] FOREIGN KEY ([LinkedDiscountId]) REFERENCES [Discount] ([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_PromotionProgram_IsPublished_EndDate] ON [PromotionProgram] ([IsPublished], [EndDate]);
    CREATE INDEX [IX_PromotionProgram_MinMemberShipId] ON [PromotionProgram] ([MinMemberShipId]);
    CREATE INDEX [IX_PromotionProgram_LinkedDiscountId] ON [PromotionProgram] ([LinkedDiscountId]);
END

IF OBJECT_ID('dbo.PromotionProgramPrice', 'U') IS NULL
BEGIN
    CREATE TABLE [PromotionProgramPrice] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [PromotionProgramId] uniqueidentifier NOT NULL,
        [ProjectionForm] int NULL,
        [SeatKind] int NULL,
        [Price] float NOT NULL DEFAULT 0,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_PromotionProgramPrice] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PromotionProgramPrice_PromotionProgram_PromotionProgramId] FOREIGN KEY ([PromotionProgramId]) REFERENCES [PromotionProgram] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_PromotionProgramPrice_PromotionProgramId_ProjectionForm_SeatKind] ON [PromotionProgramPrice] ([PromotionProgramId], [ProjectionForm], [SeatKind]);
END

IF OBJECT_ID('dbo.PromotionProgramSchedule', 'U') IS NULL
BEGIN
    CREATE TABLE [PromotionProgramSchedule] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [PromotionProgramId] uniqueidentifier NOT NULL,
        [TheaterId] uniqueidentifier NULL,
        [DaysOfWeekMask] int NULL,
        [StartTimeOfDay] time NULL,
        [EndTimeOfDay] time NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_PromotionProgramSchedule] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PromotionProgramSchedule_PromotionProgram_PromotionProgramId] FOREIGN KEY ([PromotionProgramId]) REFERENCES [PromotionProgram] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PromotionProgramSchedule_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_PromotionProgramSchedule_PromotionProgramId] ON [PromotionProgramSchedule] ([PromotionProgramId]);
    CREATE INDEX [IX_PromotionProgramSchedule_TheaterId] ON [PromotionProgramSchedule] ([TheaterId]);
END

IF COL_LENGTH('dbo.Invoice', 'PromotionProgramId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [PromotionProgramId] uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Invoice_PromotionProgram_PromotionProgramId')
BEGIN
    EXEC('ALTER TABLE [Invoice] ADD CONSTRAINT [FK_Invoice_PromotionProgram_PromotionProgramId] FOREIGN KEY ([PromotionProgramId]) REFERENCES [PromotionProgram] ([Id]) ON DELETE SET NULL');
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoice_PromotionProgramId' AND object_id = OBJECT_ID('dbo.Invoice'))
BEGIN
    EXEC('CREATE INDEX [IX_Invoice_PromotionProgramId] ON [Invoice] ([PromotionProgramId])');
END
-- ===== end promotion programs =====

-- ============================================================
-- Movie friendly-URL slug
-- Identical in create_db.sql and upgrade_db.sql (idempotent). Backfill + dedupe run through
-- sp_executesql so they compile after the column exists, matching the pattern used for
-- DayOfWeek/TheaterId above. On a fresh database the backfill is a no-op (no rows yet).
-- ============================================================
IF COL_LENGTH('dbo.Movie', 'Slug') IS NULL
BEGIN
    ALTER TABLE [Movie] ADD [Slug] nvarchar(320) NULL;
END

DECLARE @movieSlugVnFrom nvarchar(100) = N'áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđäåëîïöûüñç';
DECLARE @movieSlugVnTo   nvarchar(100) =
    REPLICATE(N'a', 17) + REPLICATE(N'e', 11) + REPLICATE(N'i', 5) + REPLICATE(N'o', 17) +
    REPLICATE(N'u', 11) + REPLICATE(N'y', 5) + N'd' +
    REPLICATE(N'a', 2) + N'e' + REPLICATE(N'i', 2) + N'o' + REPLICATE(N'u', 2) + N'n' + N'c';

DECLARE @movieSlugBackfillSql nvarchar(max) = N'
;WITH Base AS (
    SELECT [Id], TRANSLATE(LOWER(LTRIM(RTRIM([Title]))), @vnFrom, @vnTo) AS txt
    FROM [Movie]
    WHERE [Slug] IS NULL OR [Slug] = @empty
),
Tally AS (
    SELECT TOP (320) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
),
Chars AS (
    SELECT b.[Id], t.n, SUBSTRING(b.txt, t.n, 1) AS ch
    FROM Base b CROSS JOIN Tally t
    WHERE t.n <= LEN(b.txt)
),
Mapped AS (
    SELECT [Id], n, CASE WHEN ch LIKE @alnumPattern THEN ch ELSE @dash END AS mch
    FROM Chars
),
Dashed AS (
    SELECT [Id], STRING_AGG(mch, @empty) WITHIN GROUP (ORDER BY n) AS dashed
    FROM Mapped
    GROUP BY [Id]
),
Collapsed AS (
    SELECT [Id],
        TRIM(@dash FROM REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(dashed,
            @doubleDash, @dash), @doubleDash, @dash), @doubleDash, @dash), @doubleDash, @dash),
            @doubleDash, @dash), @doubleDash, @dash), @doubleDash, @dash), @doubleDash, @dash), @doubleDash, @dash)) AS collapsed
    FROM Dashed
),
BaseSlugs AS (
    SELECT [Id],
        CASE
            WHEN collapsed IS NULL OR LEFT(collapsed, 300) = @empty THEN @moviePattern
            WHEN TRY_CONVERT(uniqueidentifier, TRIM(@dash FROM LEFT(collapsed, 300))) IS NOT NULL
                THEN @moviePrefix + TRIM(@dash FROM LEFT(collapsed, 300))
            ELSE TRIM(@dash FROM LEFT(collapsed, 300))
        END AS baseSlug
    FROM Collapsed
)
UPDATE m
SET m.[Slug] = bs.baseSlug
FROM [Movie] m
JOIN BaseSlugs bs ON bs.[Id] = m.[Id];
';
EXEC sp_executesql @movieSlugBackfillSql,
    N'@vnFrom nvarchar(100), @vnTo nvarchar(100), @empty nvarchar(10), @alnumPattern nvarchar(10), @dash nvarchar(5), @doubleDash nvarchar(5), @moviePattern nvarchar(20), @moviePrefix nvarchar(20)',
    @vnFrom = @movieSlugVnFrom, @vnTo = @movieSlugVnTo, @empty = N'', @alnumPattern = N'[a-z0-9]', @dash = N'-', @doubleDash = N'--', @moviePattern = N'movie', @moviePrefix = N'movie-';

-- Safety net: a blank/whitespace-only Title produces no characters to slugify, so the CTE chain
-- above drops that row entirely and its Slug is still NULL here. Title is required at the
-- application layer, so this only guards old/bad data; it runs before dedup so any resulting
-- collisions still get a numeric suffix below.
DECLARE @movieSlugFallbackSql nvarchar(max) = N'UPDATE [Movie] SET [Slug] = @moviePattern WHERE [Slug] IS NULL OR [Slug] = @empty;';
EXEC sp_executesql @movieSlugFallbackSql, N'@moviePattern nvarchar(20), @empty nvarchar(10)', @moviePattern = N'movie', @empty = N'';

-- First collision pass: numeric suffix by creation order.
DECLARE @movieSlugDedupe1Sql nvarchar(max) = N'
;WITH Dupes AS (
    SELECT [Id], [Slug], ROW_NUMBER() OVER (PARTITION BY [Slug] ORDER BY [CreationTime], [Id]) AS rn
    FROM [Movie]
)
UPDATE m
SET m.[Slug] = m.[Slug] + @dash + CAST(d.rn AS nvarchar(10))
FROM [Movie] m
JOIN Dupes d ON d.[Id] = m.[Id] AND d.rn > 1;
';
EXEC sp_executesql @movieSlugDedupe1Sql, N'@dash nvarchar(5)', @dash = N'-';

-- Safety pass: a numeric suffix above can itself collide with an already-taken slug (e.g. "Wicked"
-- and "Wicked 2" both landing on "wicked-2"). Disambiguate any survivors with part of their own Id.
DECLARE @movieSlugDedupe2Sql nvarchar(max) = N'
;WITH StillDupes AS (
    SELECT [Id], [Slug], ROW_NUMBER() OVER (PARTITION BY [Slug] ORDER BY [CreationTime], [Id]) AS rn
    FROM [Movie]
)
UPDATE m
SET m.[Slug] = m.[Slug] + @dash + LOWER(LEFT(CONVERT(nvarchar(36), m.[Id]), 8))
FROM [Movie] m
JOIN StillDupes d ON d.[Id] = m.[Id] AND d.rn > 1;
';
EXEC sp_executesql @movieSlugDedupe2Sql, N'@dash nvarchar(5)', @dash = N'-';

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Movie') AND name = 'Slug' AND is_nullable = 1)
BEGIN
    EXEC('ALTER TABLE [Movie] ALTER COLUMN [Slug] nvarchar(320) NOT NULL');
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Movie_Slug' AND object_id = OBJECT_ID('dbo.Movie'))
BEGIN
    EXEC('CREATE UNIQUE INDEX [IX_Movie_Slug] ON [Movie] ([Slug])');
END
-- ===== end movie slug =====

-- ============================================================
-- Person directory: name -> photo lookup for Cast & Crew (no FKs)
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). Name-keyed lookup only: no
-- relation to Movie, Director or Cast. Used to resolve a photo for a cast/director name.
IF OBJECT_ID('dbo.Person', 'U') IS NULL
BEGIN
    CREATE TABLE [Person] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Name] nvarchar(200) NOT NULL,
        [PhotoUrl] nvarchar(500) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_Person] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Person_Name] ON [Person] ([Name]);
END
-- ===== end person directory =====

-- ============================================================
-- MovieCredit: explicit Person <-> Movie credits (Director/Actor, ordered, optional character name)
-- ============================================================
-- Identical in create_db.sql and upgrade_db.sql (idempotent). Movie.Director/Movie.Cast stay as
-- plain text and are unaffected; when a movie has MovieCredit rows the app prefers those.
IF OBJECT_ID('dbo.MovieCredit', 'U') IS NULL
BEGIN
    CREATE TABLE [MovieCredit] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [MovieId] uniqueidentifier NOT NULL,
        [PersonId] uniqueidentifier NOT NULL,
        [CreditType] int NOT NULL,
        [CharacterName] nvarchar(200) NULL,
        [DisplayOrder] int NOT NULL DEFAULT 0,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_MovieCredit] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MovieCredit_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MovieCredit_Person_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [Person] ([Id])
    );
    CREATE UNIQUE INDEX [IX_MovieCredit_MovieId_PersonId_CreditType] ON [MovieCredit] ([MovieId], [PersonId], [CreditType]);
    CREATE INDEX [IX_MovieCredit_PersonId] ON [MovieCredit] ([PersonId]);
END
-- ===== end movie credit =====

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
