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
--     ALTER TABLE [Movies] ADD [Rating] decimal(3,1) NULL;
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

PRINT 'upgrade_db.sql: no pending upgrades.';
