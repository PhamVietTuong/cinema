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

-- ── promotions scope (global vs per-theater) ───────────────────────────────────
IF COL_LENGTH('[Discount]', 'TheaterId') IS NULL
BEGIN
    ALTER TABLE [Discount] ADD [TheaterId] uniqueidentifier NULL;
    ALTER TABLE [Discount] ADD CONSTRAINT [FK_Discount_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE SET NULL;
    CREATE INDEX [IX_Discount_TheaterId] ON [Discount] ([TheaterId]);
    PRINT 'Added [Discount].[TheaterId] (null = system-wide).';
END

PRINT 'upgrade_db.sql: completed.';
