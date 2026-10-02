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
