-- ============================================================
-- upgrade_db.sql  —  Schema upgrade script
-- Run this on an existing Cinema database when updating
-- Add new ALTER TABLE / CREATE TABLE statements below.
-- Each change should be guarded with IF NOT EXISTS checks.
-- ============================================================

-- ── v1.x → seat-type pricing model (remove ticket types) ────────────────────
-- Price is now SeatType.PriceMultiplier × ShowTimeRoom.BasePrice; double seats
-- are two Seat rows sharing a SeatGroupId. TicketType + SeatTypeTicketType go away.
PRINT 'upgrade: applying seat-type pricing model...';

-- 1) SeatType.PriceMultiplier (default 1.0 keeps existing seats priced as before)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'SeatType' AND COLUMN_NAME = 'PriceMultiplier')
BEGIN
    ALTER TABLE [SeatType] ADD [PriceMultiplier] float NOT NULL CONSTRAINT [DF_SeatType_PriceMultiplier] DEFAULT 1;
END

-- 2) Seat.SeatGroupId (links the two halves of a double seat)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'Seat' AND COLUMN_NAME = 'SeatGroupId')
BEGIN
    ALTER TABLE [Seat] ADD [SeatGroupId] uniqueidentifier NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Seat_SeatGroupId' AND object_id = OBJECT_ID('Seat'))
    CREATE INDEX [IX_Seat_SeatGroupId] ON [Seat] ([SeatGroupId]);

-- 3) Drop InvoiceTicket → TicketType FK + column (seed multipliers onto SeatType first if needed)
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoiceTicket_TicketType_TicketTypeId')
    ALTER TABLE [InvoiceTicket] DROP CONSTRAINT [FK_InvoiceTicket_TicketType_TicketTypeId];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceTicket_TicketTypeId' AND object_id = OBJECT_ID('InvoiceTicket'))
    DROP INDEX [IX_InvoiceTicket_TicketTypeId] ON [InvoiceTicket];
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_NAME = 'InvoiceTicket' AND COLUMN_NAME = 'TicketTypeId')
    ALTER TABLE [InvoiceTicket] DROP COLUMN [TicketTypeId];

-- 4) Drop the SeatTypeTicketType price matrix, then the TicketType table
IF OBJECT_ID('SeatTypeTicketType', 'U') IS NOT NULL DROP TABLE [SeatTypeTicketType];
IF OBJECT_ID('TicketType', 'U') IS NOT NULL DROP TABLE [TicketType];

PRINT 'upgrade: seat-type pricing model applied.';


-- ── v1.0 → v1.1 template (remove and fill in real changes) ──

-- Example: add a new column to an existing table
-- IF NOT EXISTS (
--     SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
--     WHERE TABLE_NAME = 'Movies' AND COLUMN_NAME = 'Rating'
-- )
-- BEGIN
--     ALTER TABLE [Movies] ADD [Rating] float NULL;
-- END

-- Example: add a new index
-- IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Movies_ReleaseDate' AND object_id = OBJECT_ID('Movies'))
-- BEGIN
--     CREATE INDEX [IX_Movies_ReleaseDate] ON [Movies] ([ReleaseDate]);
-- END

-- Example: create a new table
-- IF OBJECT_ID('NewTable') IS NULL
-- BEGIN
--     CREATE TABLE [NewTable] (
--         [Id] int NOT NULL IDENTITY,
--         [Name] nvarchar(200) NOT NULL,
--         [CreationTime] datetime NOT NULL,
--         CONSTRAINT [PK_NewTable] PRIMARY KEY ([Id])
--     );
-- END

-- ── Convert money columns from decimal/numeric to float ──────────────────────
-- The CLR entities use `double`, which EF maps to SQL `float`. Older databases
-- may still have these columns typed decimal/numeric; convert them. Idempotent:
-- only columns currently typed decimal/numeric are altered, and nullability is
-- preserved.

DECLARE @moneyCols TABLE (TableName sysname, ColumnName sysname, IsNullable bit);
INSERT INTO @moneyCols (TableName, ColumnName, IsNullable) VALUES
    ('FoodAndDrink',        'Price',             0),
    ('Holiday',             'PriceMultiplier',   0),
    ('MemberShip',          'DiscountPercent',   0),
    ('TicketType',          'BasePrice',         0),
    ('Discount',            'Percent',           0),
    ('Discount',            'MaxDiscountAmount', 1),
    ('SeatTypeTicketType',  'PriceMultiplier',   0),
    ('Invoice',             'TotalAmount',       0),
    ('Invoice',             'DiscountAmount',    0),
    ('Invoice',             'FinalAmount',       0),
    ('InvoiceFoodAndDrink', 'UnitPrice',         0),
    ('InvoiceFoodAndDrink', 'TotalPrice',        0),
    ('InvoiceTicket',       'Price',             0);

DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql
    + N'ALTER TABLE [' + c.TableName + N'] ALTER COLUMN [' + c.ColumnName + N'] float '
    + CASE WHEN c.IsNullable = 1 THEN N'NULL' ELSE N'NOT NULL' END + N';' + CHAR(13) + CHAR(10)
FROM @moneyCols c
JOIN INFORMATION_SCHEMA.COLUMNS ic
    ON ic.TABLE_NAME = c.TableName AND ic.COLUMN_NAME = c.ColumnName
WHERE ic.DATA_TYPE IN ('decimal', 'numeric');

IF @sql <> N''
BEGIN
    PRINT 'Converting decimal/numeric money columns to float...';
    EXEC sp_executesql @sql;
    PRINT 'Done.';
END
ELSE
BEGIN
    PRINT 'Money columns already float — nothing to convert.';
END

-- Password reset support on [User] (added for the forgot-password flow).
IF COL_LENGTH('[User]', 'PasswordResetTokenHash') IS NULL
BEGIN
    ALTER TABLE [User] ADD [PasswordResetTokenHash] nvarchar(max) NULL;
    PRINT 'Added [User].[PasswordResetTokenHash].';
END
IF COL_LENGTH('[User]', 'PasswordResetExpiresAt') IS NULL
BEGIN
    ALTER TABLE [User] ADD [PasswordResetExpiresAt] datetime NULL;
    PRINT 'Added [User].[PasswordResetExpiresAt].';
END

-- Account lockout support on [User] (Batch B).
IF COL_LENGTH('[User]', 'FailedLoginCount') IS NULL
BEGIN
    ALTER TABLE [User] ADD [FailedLoginCount] int NOT NULL DEFAULT 0;
    PRINT 'Added [User].[FailedLoginCount].';
END
IF COL_LENGTH('[User]', 'LockoutEndUtc') IS NULL
BEGIN
    ALTER TABLE [User] ADD [LockoutEndUtc] datetime NULL;
    PRINT 'Added [User].[LockoutEndUtc].';
END

-- Email verification support on [User] (Batch E).
IF COL_LENGTH('[User]', 'EmailConfirmed') IS NULL
BEGIN
    ALTER TABLE [User] ADD [EmailConfirmed] bit NOT NULL DEFAULT 0;
    -- Grandfather in all pre-existing accounts so they aren't locked out by the new gate.
    EXEC('UPDATE [User] SET [EmailConfirmed] = 1');
    PRINT 'Added [User].[EmailConfirmed] (existing users grandfathered as confirmed).';
END
IF COL_LENGTH('[User]', 'EmailVerificationTokenHash') IS NULL
BEGIN
    ALTER TABLE [User] ADD [EmailVerificationTokenHash] nvarchar(max) NULL;
    PRINT 'Added [User].[EmailVerificationTokenHash].';
END
IF COL_LENGTH('[User]', 'EmailVerificationExpiresAt') IS NULL
BEGIN
    ALTER TABLE [User] ADD [EmailVerificationExpiresAt] datetime NULL;
    PRINT 'Added [User].[EmailVerificationExpiresAt].';
END

-- Two-factor authentication support on [User] (Batch D).
IF COL_LENGTH('[User]', 'TwoFactorEnabled') IS NULL
BEGIN
    ALTER TABLE [User] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT 0;
    PRINT 'Added [User].[TwoFactorEnabled].';
END
IF COL_LENGTH('[User]', 'TwoFactorCodeHash') IS NULL
BEGIN
    ALTER TABLE [User] ADD [TwoFactorCodeHash] nvarchar(max) NULL;
    PRINT 'Added [User].[TwoFactorCodeHash].';
END
IF COL_LENGTH('[User]', 'TwoFactorCodeExpiresAt') IS NULL
BEGIN
    ALTER TABLE [User] ADD [TwoFactorCodeExpiresAt] datetime NULL;
    PRINT 'Added [User].[TwoFactorCodeExpiresAt].';
END

-- ── per-theater catalog + ticket pricing ───────────────────────────────────────
-- Seat types & food/drinks become per-theater (add TheaterId); the old
-- FoodAndDrinkTheater availability join is removed; TimeSlot + TicketPrice added.
-- NOTE: for an existing DB, this assigns all current seat types / food to the FIRST
-- theater as a backfill. A clean per-theater split is best done by reseeding.
PRINT 'upgrade: applying per-theater catalog + ticket pricing...';

DECLARE @FirstTheater uniqueidentifier = (SELECT TOP 1 [Id] FROM [Theater] ORDER BY [CreationTime]);

-- 1) SeatType.TheaterId
IF COL_LENGTH('[SeatType]', 'TheaterId') IS NULL
BEGIN
    ALTER TABLE [SeatType] ADD [TheaterId] uniqueidentifier NULL;
    EXEC('UPDATE [SeatType] SET [TheaterId] = ' + '(SELECT TOP 1 [Id] FROM [Theater] ORDER BY [CreationTime]) WHERE [TheaterId] IS NULL');
    ALTER TABLE [SeatType] ALTER COLUMN [TheaterId] uniqueidentifier NOT NULL;
    ALTER TABLE [SeatType] ADD CONSTRAINT [FK_SeatType_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE;
    CREATE INDEX [IX_SeatType_TheaterId] ON [SeatType] ([TheaterId]);
    PRINT 'Added [SeatType].[TheaterId].';
END

-- 2) FoodAndDrink.TheaterId
IF COL_LENGTH('[FoodAndDrink]', 'TheaterId') IS NULL
BEGIN
    ALTER TABLE [FoodAndDrink] ADD [TheaterId] uniqueidentifier NULL;
    EXEC('UPDATE [FoodAndDrink] SET [TheaterId] = ' + '(SELECT TOP 1 [Id] FROM [Theater] ORDER BY [CreationTime]) WHERE [TheaterId] IS NULL');
    ALTER TABLE [FoodAndDrink] ALTER COLUMN [TheaterId] uniqueidentifier NOT NULL;
    ALTER TABLE [FoodAndDrink] ADD CONSTRAINT [FK_FoodAndDrink_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE;
    CREATE INDEX [IX_FoodAndDrink_TheaterId] ON [FoodAndDrink] ([TheaterId]);
    PRINT 'Added [FoodAndDrink].[TheaterId].';
END

-- 3) Drop the old FoodAndDrinkTheater availability join
IF OBJECT_ID('FoodAndDrinkTheater', 'U') IS NOT NULL
BEGIN
    DROP TABLE [FoodAndDrinkTheater];
    PRINT 'Dropped [FoodAndDrinkTheater].';
END

-- 4) TimeSlot
IF OBJECT_ID('TimeSlot', 'U') IS NULL
BEGIN
    CREATE TABLE [TimeSlot] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [StartTime] nvarchar(5) NOT NULL,
        [EndTime] nvarchar(5) NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_TimeSlot] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeSlot_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TimeSlot_TheaterId] ON [TimeSlot] ([TheaterId]);
    PRINT 'Created [TimeSlot].';
END

-- 5) TicketPrice
IF OBJECT_ID('TicketPrice', 'U') IS NULL
BEGIN
    CREATE TABLE [TicketPrice] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [SeatTypeId] uniqueidentifier NOT NULL,
        [TimeSlotId] uniqueidentifier NOT NULL,
        [IsHoliday] bit NOT NULL,
        [Price] float NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_TicketPrice] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketPrice_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TicketPrice_SeatType_SeatTypeId] FOREIGN KEY ([SeatTypeId]) REFERENCES [SeatType] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketPrice_TimeSlot_TimeSlotId] FOREIGN KEY ([TimeSlotId]) REFERENCES [TimeSlot] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_TicketPrice_SeatTypeId] ON [TicketPrice] ([SeatTypeId]);
    CREATE INDEX [IX_TicketPrice_TimeSlotId] ON [TicketPrice] ([TimeSlotId]);
    CREATE UNIQUE INDEX [IX_TicketPrice_TheaterId_SeatTypeId_TimeSlotId_IsHoliday] ON [TicketPrice] ([TheaterId], [SeatTypeId], [TimeSlotId], [IsHoliday]);
    PRINT 'Created [TicketPrice].';
END

PRINT 'upgrade: per-theater catalog + ticket pricing applied.';

-- ── screening room types (2D/3D/IMAX/4DX) ──────────────────────────────────────
-- Room type drives ticket price (4th dimension) + equipment. Backfills existing
-- rows to a default per-theater "2D" type.
PRINT 'upgrade: applying screening room types...';

-- 1) RoomType table (+ a default 2D per theater to backfill existing rooms)
IF OBJECT_ID('RoomType', 'U') IS NULL
BEGIN
    CREATE TABLE [RoomType] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [TheaterId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_RoomType] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomType_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_RoomType_TheaterId] ON [RoomType] ([TheaterId]);
    INSERT INTO [RoomType] ([Id], [TheaterId], [Name], [Description], [CreationTime])
    SELECT NEWID(), t.[Id], N'2D', N'Standard 2D projection', GETUTCDATE() FROM [Theater] t;
    PRINT 'Created [RoomType] + seeded default 2D per theater.';
END

-- 2) Room.RoomTypeId (backfill each room to its theater's 2D type)
IF COL_LENGTH('[Room]', 'RoomTypeId') IS NULL
BEGIN
    ALTER TABLE [Room] ADD [RoomTypeId] uniqueidentifier NULL;
    EXEC('UPDATE r SET r.[RoomTypeId] = (SELECT TOP 1 rt.[Id] FROM [RoomType] rt WHERE rt.[TheaterId] = r.[TheaterId]) FROM [Room] r WHERE r.[RoomTypeId] IS NULL');
    ALTER TABLE [Room] ALTER COLUMN [RoomTypeId] uniqueidentifier NOT NULL;
    ALTER TABLE [Room] ADD CONSTRAINT [FK_Room_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]);
    CREATE INDEX [IX_Room_RoomTypeId] ON [Room] ([RoomTypeId]);
    PRINT 'Added [Room].[RoomTypeId].';
END

-- 3) TicketPrice.RoomTypeId (rebuild the unique index to include it)
IF COL_LENGTH('[TicketPrice]', 'RoomTypeId') IS NULL
BEGIN
    ALTER TABLE [TicketPrice] ADD [RoomTypeId] uniqueidentifier NULL;
    EXEC('UPDATE p SET p.[RoomTypeId] = (SELECT TOP 1 rt.[Id] FROM [RoomType] rt WHERE rt.[TheaterId] = p.[TheaterId]) FROM [TicketPrice] p WHERE p.[RoomTypeId] IS NULL');
    ALTER TABLE [TicketPrice] ALTER COLUMN [RoomTypeId] uniqueidentifier NOT NULL;
    ALTER TABLE [TicketPrice] ADD CONSTRAINT [FK_TicketPrice_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]);
    CREATE INDEX [IX_TicketPrice_RoomTypeId] ON [TicketPrice] ([RoomTypeId]);
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketPrice_TheaterId_SeatTypeId_TimeSlotId_IsHoliday' AND object_id = OBJECT_ID('TicketPrice'))
        DROP INDEX [IX_TicketPrice_TheaterId_SeatTypeId_TimeSlotId_IsHoliday] ON [TicketPrice];
    CREATE UNIQUE INDEX [IX_TicketPrice_TheaterId_RoomTypeId_SeatTypeId_TimeSlotId_IsHoliday] ON [TicketPrice] ([TheaterId], [RoomTypeId], [SeatTypeId], [TimeSlotId], [IsHoliday]);
    PRINT 'Added [TicketPrice].[RoomTypeId].';
END

PRINT 'upgrade: screening room types applied.';

-- ── promotions scope (multi-theater / movie / day + time-of-day, auto-apply) ────
-- New promotion columns on [Discount]. EXEC() defers name resolution so statements
-- referencing the just-added columns parse cleanly in this single batch.
IF COL_LENGTH('[Discount]', 'AutoApply') IS NULL
BEGIN
    ALTER TABLE [Discount] ADD
        [AutoApply] bit NOT NULL CONSTRAINT [DF_Discount_AutoApply] DEFAULT 0,
        [ApplyToAllTheaters] bit NOT NULL CONSTRAINT [DF_Discount_ApplyToAllTheaters] DEFAULT 1,
        [MovieId] uniqueidentifier NULL,
        [DaysOfWeekMask] int NULL,
        [StartTimeOfDay] time NULL,
        [EndTimeOfDay] time NULL;
    PRINT 'Added promotion scope columns to [Discount].';
END

IF OBJECT_ID('FK_Discount_Movie_MovieId', 'F') IS NULL
    ALTER TABLE [Discount] ADD CONSTRAINT [FK_Discount_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE SET NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Discount_MovieId' AND object_id = OBJECT_ID('Discount'))
    CREATE INDEX [IX_Discount_MovieId] ON [Discount] ([MovieId]);

-- [Code] becomes optional (auto-apply promotions have no code); keep it unique when present.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Discount') AND name = 'Code' AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Discount_Code' AND object_id = OBJECT_ID('Discount'))
        DROP INDEX [IX_Discount_Code] ON [Discount];
    ALTER TABLE [Discount] ALTER COLUMN [Code] nvarchar(50) NULL;
    CREATE UNIQUE INDEX [IX_Discount_Code] ON [Discount] ([Code]) WHERE [Code] IS NOT NULL;
    PRINT 'Made [Discount].[Code] nullable with filtered-unique index.';
END

-- Join table for per-theater promotion scope.
IF OBJECT_ID('[DiscountTheater]', 'U') IS NULL
BEGIN
    CREATE TABLE [DiscountTheater] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [DiscountId] uniqueidentifier NOT NULL,
        [TheaterId] uniqueidentifier NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_DiscountTheater] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DiscountTheater_Discount_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discount] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DiscountTheater_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX [IX_DiscountTheater_DiscountId_TheaterId] ON [DiscountTheater] ([DiscountId], [TheaterId]);
    CREATE INDEX [IX_DiscountTheater_TheaterId] ON [DiscountTheater] ([TheaterId]);
    PRINT 'Created [DiscountTheater].';
END

-- Migrate the old single-theater scope into the join table, then drop [Discount].[TheaterId].
IF COL_LENGTH('[Discount]', 'TheaterId') IS NOT NULL
BEGIN
    EXEC('INSERT INTO [DiscountTheater] ([Id], [DiscountId], [TheaterId], [CreationTime])
          SELECT NEWID(), d.[Id], d.[TheaterId], GETUTCDATE()
          FROM [Discount] d
          WHERE d.[TheaterId] IS NOT NULL
            AND NOT EXISTS (SELECT 1 FROM [DiscountTheater] dt WHERE dt.[DiscountId] = d.[Id] AND dt.[TheaterId] = d.[TheaterId])');
    -- A theater-scoped code is no longer system-wide.
    EXEC('UPDATE [Discount] SET [ApplyToAllTheaters] = 0 WHERE [TheaterId] IS NOT NULL');

    IF OBJECT_ID('FK_Discount_Theater_TheaterId', 'F') IS NOT NULL
        ALTER TABLE [Discount] DROP CONSTRAINT [FK_Discount_Theater_TheaterId];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Discount_TheaterId' AND object_id = OBJECT_ID('Discount'))
        DROP INDEX [IX_Discount_TheaterId] ON [Discount];
    ALTER TABLE [Discount] DROP COLUMN [TheaterId];
    PRINT 'Migrated [Discount].[TheaterId] into [DiscountTheater] and dropped the column.';
END

-- ── theater geo-coordinates (nearest-theater search) ───────────────────────────
IF COL_LENGTH('[Theater]', 'Latitude') IS NULL
BEGIN
    ALTER TABLE [Theater] ADD [Latitude] float NULL, [Longitude] float NULL;
    PRINT 'Added [Theater].[Latitude]/[Longitude].';
END

-- ── theater-staff role ─────────────────────────────────────────────────────────
IF COL_LENGTH('[User]', 'TheaterId') IS NULL
BEGIN
    ALTER TABLE [User] ADD [TheaterId] uniqueidentifier NULL;
    ALTER TABLE [User] ADD CONSTRAINT [FK_User_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE SET NULL;
    CREATE INDEX [IX_User_TheaterId] ON [User] ([TheaterId]);
    PRINT 'Added [User].[TheaterId].';
END
IF NOT EXISTS (SELECT 1 FROM [UserType] WHERE [Name] = N'TheaterStaff')
    INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES (NEWID(), N'TheaterStaff', GETUTCDATE());

-- ── refund flow ─────────────────────────────────────────────────────────────────
-- Records when a Paid invoice was refunded (InvoiceStatus.Refunded = 4). Refunding
-- frees the seats (seat occupancy counts only Pending/Paid invoices) and reverses
-- the loyalty points and promo-code usage accrued at payment.
IF COL_LENGTH('[Invoice]', 'RefundedAt') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [RefundedAt] datetime NULL;
    PRINT 'Added [Invoice].[RefundedAt].';
END

-- ── loyalty redemption ──────────────────────────────────────────────────────────
-- Points spent on a booking, reserved at creation and restored on cancel/expire/refund.
IF COL_LENGTH('[Invoice]', 'PointsRedeemed') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [PointsRedeemed] int NOT NULL CONSTRAINT [DF_Invoice_PointsRedeemed] DEFAULT 0;
    PRINT 'Added [Invoice].[PointsRedeemed].';
END

-- ── notification preferences (opt-out, default on) ──────────────────────────────
IF COL_LENGTH('[User]', 'NotifyBookingEmails') IS NULL
BEGIN
    ALTER TABLE [User] ADD [NotifyBookingEmails] bit NOT NULL CONSTRAINT [DF_User_NotifyBookingEmails] DEFAULT 1;
    PRINT 'Added [User].[NotifyBookingEmails].';
END
IF COL_LENGTH('[User]', 'NotifyPromotionEmails') IS NULL
BEGIN
    ALTER TABLE [User] ADD [NotifyPromotionEmails] bit NOT NULL CONSTRAINT [DF_User_NotifyPromotionEmails] DEFAULT 1;
    PRINT 'Added [User].[NotifyPromotionEmails].';
END
IF COL_LENGTH('[User]', 'NotifyReminderEmails') IS NULL
BEGIN
    ALTER TABLE [User] ADD [NotifyReminderEmails] bit NOT NULL CONSTRAINT [DF_User_NotifyReminderEmails] DEFAULT 1;
    PRINT 'Added [User].[NotifyReminderEmails].';
END

-- ── cross-instance double-booking guard (active-seat unique index) ───────────────
-- A ticket is active while its invoice is Pending/Paid; a filtered unique index over active tickets
-- means two server instances can't double-sell the same (showtime, room, seat).
IF COL_LENGTH('[InvoiceTicket]', 'IsActive') IS NULL
BEGIN
    ALTER TABLE [InvoiceTicket] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_InvoiceTicket_IsActive] DEFAULT 1;
    PRINT 'Added [InvoiceTicket].[IsActive].';
    -- Backfill: tickets on Cancelled/Failed/Refunded invoices are inactive (0=Pending, 1=Paid stay active).
    UPDATE it SET it.[IsActive] = 0
    FROM [InvoiceTicket] it JOIN [Invoice] i ON i.[Id] = it.[InvoiceId]
    WHERE i.[Status] NOT IN (0, 1);
    PRINT 'Backfilled [InvoiceTicket].[IsActive] from invoice status.';
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoiceTicket_ActiveSeat' AND object_id = OBJECT_ID('InvoiceTicket'))
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceTicket_ActiveSeat] ON [InvoiceTicket] ([ShowTimeId], [RoomId], [SeatId]) WHERE [IsActive] = 1;
    PRINT 'Created [IX_InvoiceTicket_ActiveSeat].';
END

-- ── restart-safe showtime reminders ──────────────────────────────────────────────
-- Persists which (user, showtime) reminders were sent so a process restart doesn't re-send.
IF OBJECT_ID('ReminderLog', 'U') IS NULL
BEGIN
    CREATE TABLE [ReminderLog] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [UserId] uniqueidentifier NOT NULL,
        [ShowTimeId] uniqueidentifier NOT NULL,
        [SentAt] datetime NOT NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_ReminderLog] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ReminderLog_UserId_ShowTimeId] ON [ReminderLog] ([UserId], [ShowTimeId]);
    PRINT 'Created [ReminderLog].';
END

-- ── gift cards / vouchers ────────────────────────────────────────────────────────
IF OBJECT_ID('GiftCard', 'U') IS NULL
BEGIN
    CREATE TABLE [GiftCard] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
        [Code] nvarchar(50) NOT NULL,
        [InitialBalance] float NOT NULL,
        [Balance] float NOT NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [ExpiresAt] datetime NULL,
        [IssuedToEmail] nvarchar(max) NULL,
        [CreationTime] datetime NOT NULL,
        [LastUpdatedTime] datetime NULL,
        CONSTRAINT [PK_GiftCard] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_GiftCard_Code] ON [GiftCard] ([Code]);
    PRINT 'Created [GiftCard].';
END
IF COL_LENGTH('[Invoice]', 'GiftCardId') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [GiftCardId] uniqueidentifier NULL;
    PRINT 'Added [Invoice].[GiftCardId].';
END
IF COL_LENGTH('[Invoice]', 'GiftCardAmount') IS NULL
BEGIN
    ALTER TABLE [Invoice] ADD [GiftCardAmount] float NOT NULL CONSTRAINT [DF_Invoice_GiftCardAmount] DEFAULT 0;
    PRINT 'Added [Invoice].[GiftCardAmount].';
END

PRINT 'upgrade_db.sql: completed.';

-- ── Adopt EF Core migrations (baseline) ─────────────────────────────────────
-- One-time step for databases created before migrations existed. Everything above this
-- line brings the schema to the same shape as the InitialBaseline migration, so we stamp
-- that migration as already applied rather than running it. From here on, schema changes
-- come from `dotnet ef migrations add` and are applied with `dotnet ef database update`.
PRINT 'upgrade: stamping EF Core migrations baseline...';

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
