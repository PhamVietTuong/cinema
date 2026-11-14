-- assert-after-upgrade.sql: verifies the state upgrade_db.sql must leave behind. Run after every upgrade pass:
--   sqlcmd ... -v Phase=first   (also saves a row-count snapshot)
--   sqlcmd ... -v Phase=second  (additionally asserts nothing changed since the snapshot)
-- Every failed check THROWs, so `sqlcmd -b` exits non-zero.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @InvGift uniqueidentifier = '20000000-0000-0000-0000-000000000001';
DECLARE @InvGiftPending uniqueidentifier = '20000000-0000-0000-0000-000000000002';
DECLARE @InvChild uniqueidentifier = '20000000-0000-0000-0000-000000000003';
DECLARE @InvOther uniqueidentifier = '20000000-0000-0000-0000-000000000004';
DECLARE @InvSurvivor uniqueidentifier = '20000000-0000-0000-0000-000000000005';
DECLARE @Purged TABLE ([Id] uniqueidentifier);
INSERT INTO @Purged VALUES (@InvGift), (@InvGiftPending), (@InvChild);
DECLARE @n int;

-- 1. gift-card and child invoices and everything under them are gone
SELECT @n = COUNT(*) FROM [Invoice] WHERE [Id] IN (SELECT [Id] FROM @Purged);
IF @n <> 0 BEGIN THROW 50001, 'purged invoices still exist', 1; END
SELECT @n = (SELECT COUNT(*) FROM [InvoiceTicket] WHERE [InvoiceId] IN (SELECT [Id] FROM @Purged))
          + (SELECT COUNT(*) FROM [InvoicePayment] WHERE [InvoiceId] IN (SELECT [Id] FROM @Purged))
          + (SELECT COUNT(*) FROM [InvoiceFoodAndDrink] WHERE [InvoiceId] IN (SELECT [Id] FROM @Purged))
          + (SELECT COUNT(*) FROM [StockMovement] WHERE [InvoiceId] IN (SELECT [Id] FROM @Purged));
IF @n <> 0 BEGIN THROW 50002, 'children of purged invoices still exist (ticket/payment/food/stock)', 1; END
PRINT 'ok: gift-card and child invoices purged with tickets, payments, food lines, stock movements';

-- 2. the unrelated invoice and its children survive
IF NOT EXISTS (SELECT 1 FROM [Invoice] WHERE [Id] = @InvOther) BEGIN THROW 50003, 'unrelated invoice was deleted', 1; END
IF NOT EXISTS (SELECT 1 FROM [Invoice] WHERE [Id] = @InvSurvivor) BEGIN THROW 50004, 'survivor invoice was deleted', 1; END
SELECT @n = (SELECT COUNT(*) FROM [InvoiceTicket] WHERE [InvoiceId] = @InvOther)
          + (SELECT COUNT(*) FROM [InvoicePayment] WHERE [InvoiceId] = @InvOther)
          + (SELECT COUNT(*) FROM [InvoiceFoodAndDrink] WHERE [InvoiceId] = @InvOther)
          + (SELECT COUNT(*) FROM [StockMovement] WHERE [InvoiceId] = @InvOther);
IF @n <> 4 BEGIN THROW 50005, 'unrelated invoice lost children', 1; END
SELECT @n = COUNT(*) FROM [Invoice];
IF @n <> 2 BEGIN THROW 50006, 'expected exactly 2 invoices to survive', 1; END
PRINT 'ok: unrelated invoice and its children survive';

-- 3. survivor's exchange link nulled
IF EXISTS (SELECT 1 FROM [Invoice] WHERE [Id] = @InvSurvivor AND [ExchangedFromInvoiceId] IS NOT NULL)
BEGIN THROW 50007, 'survivor ExchangedFromInvoiceId was not nulled', 1; END
PRINT 'ok: survivor exchange link is NULL';

-- 4. points of the Pending gift invoice given back (100 + 30)
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Id] = '10000000-0000-0000-0000-000000000003' AND [Points] = 130)
BEGIN THROW 50008, 'pending invoice points were not returned (expected 130)', 1; END
PRINT 'ok: pending invoice points returned to the user';

-- 5. child category, its price rows and the one-shot marker
IF EXISTS (SELECT 1 FROM [PatronCategory] WHERE [Id] = '10000000-0000-0000-0000-00000000000A') BEGIN THROW 50009, 'child category still exists', 1; END
IF NOT EXISTS (SELECT 1 FROM [PatronCategory] WHERE [Id] = '10000000-0000-0000-0000-00000000000B') BEGIN THROW 50010, 'adult category was deleted', 1; END
SELECT @n = COUNT(*) FROM [RoomTypePatronCategoryPrice];
IF @n <> 1 BEGIN THROW 50011, 'expected exactly 1 RoomTypePatronCategoryPrice row (adult) to remain', 1; END
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE [class] = 0 AND [name] = N'purge_child_ticket_done')
BEGIN THROW 50012, 'purge_child_ticket_done property missing', 1; END
PRINT 'ok: child category and price rows removed, purge_child_ticket_done set';

-- 6. gift-card and discount schema is gone
IF OBJECT_ID('dbo.GiftCard', 'U') IS NOT NULL BEGIN THROW 50013, 'GiftCard table still exists', 1; END
IF COL_LENGTH('dbo.Invoice', 'GiftCardId') IS NOT NULL OR COL_LENGTH('dbo.Invoice', 'GiftCardAmount') IS NOT NULL
BEGIN THROW 50014, 'Invoice gift-card columns still exist', 1; END
IF COL_LENGTH('dbo.MemberShip', 'DiscountPercent') IS NOT NULL BEGIN THROW 50015, 'MemberShip.DiscountPercent still exists', 1; END
IF COL_LENGTH('dbo.PromotionProgram', 'StacksWithMembershipDiscount') IS NOT NULL BEGIN THROW 50016, 'PromotionProgram.StacksWithMembershipDiscount still exists', 1; END
PRINT 'ok: GiftCard table and gift-card / discount columns dropped';

-- 7. Theater columns added with their defaults
IF NOT EXISTS (SELECT 1 FROM [Theater] WHERE [Id] = '10000000-0000-0000-0000-000000000001'
               AND [MaxSeatsPerBooking] = 10 AND [MaxPromotionDiscountPercent] = 50 AND [MaxHolidayPromotionDiscountPercent] = 50)
BEGIN THROW 50017, 'Theater MaxSeatsPerBooking / MaxPromotionDiscountPercent / MaxHolidayPromotionDiscountPercent not 10/50/50', 1; END
PRINT 'ok: Theater limits are 10 / 50 / 50';

-- 8. consent default is 0 under its name; the stored value is unchanged (old default was 1)
DECLARE @dcName sysname, @dcDef nvarchar(100);
SELECT @dcName = dc.[name], @dcDef = dc.[definition] FROM sys.default_constraints dc
JOIN sys.columns c ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
WHERE dc.[parent_object_id] = OBJECT_ID('dbo.User') AND c.[name] = 'NotifyPromotionEmails';
IF @dcName IS NULL OR @dcName <> 'DF_User_NotifyPromotionEmails' OR @dcDef <> '((0))'
BEGIN THROW 50018, 'NotifyPromotionEmails default is not ((0)) named DF_User_NotifyPromotionEmails', 1; END
IF NOT EXISTS (SELECT 1 FROM [User] WHERE [Id] = '10000000-0000-0000-0000-000000000003' AND [NotifyPromotionEmails] = 1)
BEGIN THROW 50019, 'existing user NotifyPromotionEmails value changed', 1; END
PRINT 'ok: NotifyPromotionEmails default ((0)) named DF_User_NotifyPromotionEmails, stored value unchanged';

-- 9. snapshot / idempotence
IF OBJECT_ID('dbo.HarnessSnapshot', 'U') IS NULL BEGIN CREATE TABLE [HarnessSnapshot] ([Name] varchar(60) PRIMARY KEY, [Cnt] bigint NOT NULL); END
DECLARE @now TABLE ([Name] varchar(60), [Cnt] bigint);
INSERT INTO @now VALUES
  ('Invoice', (SELECT COUNT(*) FROM [Invoice])),
  ('InvoiceTicket', (SELECT COUNT(*) FROM [InvoiceTicket])),
  ('InvoicePayment', (SELECT COUNT(*) FROM [InvoicePayment])),
  ('InvoiceFoodAndDrink', (SELECT COUNT(*) FROM [InvoiceFoodAndDrink])),
  ('StockMovement', (SELECT COUNT(*) FROM [StockMovement])),
  ('PatronCategory', (SELECT COUNT(*) FROM [PatronCategory])),
  ('RoomTypePatronCategoryPrice', (SELECT COUNT(*) FROM [RoomTypePatronCategoryPrice])),
  ('Theater', (SELECT COUNT(*) FROM [Theater])),
  ('MemberShip', (SELECT COUNT(*) FROM [MemberShip])),
  ('PromotionProgram', (SELECT COUNT(*) FROM [PromotionProgram])),
  ('User', (SELECT COUNT(*) FROM [User])),
  ('UserPoints', (SELECT SUM(CAST([Points] AS bigint)) FROM [User])),
  ('ExtendedProperties', (SELECT COUNT(*) FROM sys.extended_properties WHERE [class] = 0));
IF '$(Phase)' = 'first'
BEGIN
    DELETE FROM [HarnessSnapshot];
    INSERT INTO [HarnessSnapshot] SELECT [Name], [Cnt] FROM @now;
    PRINT 'ok: snapshot saved';
END
ELSE
BEGIN
    IF EXISTS (SELECT 1 FROM @now n LEFT JOIN [HarnessSnapshot] s ON s.[Name] = n.[Name] WHERE s.[Cnt] IS NULL OR s.[Cnt] <> n.[Cnt])
    BEGIN THROW 50020, 'second upgrade run changed row counts', 1; END
    PRINT 'ok: second run changed nothing (row counts identical)';
END
