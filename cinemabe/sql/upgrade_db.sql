-- ============================================================
-- upgrade_db.sql  —  Schema upgrade script
-- Run this on an existing Cinema database when updating
-- Add new ALTER TABLE / CREATE TABLE statements below.
-- Each change should be guarded with IF NOT EXISTS checks.
-- ============================================================

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

PRINT 'upgrade_db.sql: completed.';
