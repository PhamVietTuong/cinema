-- seed-old-schema.sql: rows the guarded blocks of upgrade_db.sql must act on, written against the OLD schema
-- (create_db.sql of the commit before the discount / gift-card / child-ticket work). Run with sqlcmd -I -b -f 65001.
-- Fixed GUIDs so assert-after-upgrade.sql can address every row. The child category name is built from NCHAR(7867),
-- never a typed non-ASCII character.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @now datetime = GETDATE();
DECLARE @Theater uniqueidentifier = '10000000-0000-0000-0000-000000000001';
DECLARE @UserType uniqueidentifier = '10000000-0000-0000-0000-000000000002';
DECLARE @User uniqueidentifier = '10000000-0000-0000-0000-000000000003';
DECLARE @Age uniqueidentifier = '10000000-0000-0000-0000-000000000004';
DECLARE @Movie uniqueidentifier = '10000000-0000-0000-0000-000000000005';
DECLARE @ShowTime uniqueidentifier = '10000000-0000-0000-0000-000000000006';
DECLARE @RoomType uniqueidentifier = '10000000-0000-0000-0000-000000000007';
DECLARE @Room uniqueidentifier = '10000000-0000-0000-0000-000000000008';
DECLARE @SeatType uniqueidentifier = '10000000-0000-0000-0000-000000000009';
DECLARE @ChildCat uniqueidentifier = '10000000-0000-0000-0000-00000000000A';
DECLARE @AdultCat uniqueidentifier = '10000000-0000-0000-0000-00000000000B';
DECLARE @Food uniqueidentifier = '10000000-0000-0000-0000-00000000000C';
DECLARE @GiftCard uniqueidentifier = '10000000-0000-0000-0000-00000000000D';
DECLARE @Member uniqueidentifier = '10000000-0000-0000-0000-00000000000E';
DECLARE @Promo uniqueidentifier = '10000000-0000-0000-0000-00000000000F';
-- invoices
DECLARE @InvGift uniqueidentifier = '20000000-0000-0000-0000-000000000001';        -- Paid, gift card, 2 tickets
DECLARE @InvGiftPending uniqueidentifier = '20000000-0000-0000-0000-000000000002'; -- Pending, gift card, 30 points
DECLARE @InvChild uniqueidentifier = '20000000-0000-0000-0000-000000000003';       -- Paid, 2 child tickets
DECLARE @InvOther uniqueidentifier = '20000000-0000-0000-0000-000000000004';       -- unrelated
DECLARE @InvSurvivor uniqueidentifier = '20000000-0000-0000-0000-000000000005';    -- exchanged from the child invoice
DECLARE @ChildName nvarchar(100) = N'Tr' + NCHAR(7867) + N' Em';

INSERT INTO [Theater] ([Id],[Name],[Address],[City],[IsActive],[CreationTime])
VALUES (@Theater, N'Harness Theater', N'1 Test St', N'Hanoi', 1, @now);
INSERT INTO [UserType] ([Id],[Name],[CreationTime]) VALUES (@UserType, N'Customer', @now);
INSERT INTO [MemberShip] ([Id],[Name],[MinPoints],[MaxPoints],[DiscountPercent],[CreationTime])
VALUES (@Member, N'Silver', 0, 999, 5, @now);
-- NotifyPromotionEmails is left to the OLD default (1): the upgrade must change the default, never the stored value.
INSERT INTO [User] ([Id],[Phone],[Email],[Name],[PasswordHash],[PasswordSalt],[Status],[UserTypeId],[MemberShipId],[Points],[CreationTime])
VALUES (@User, N'0900000000', N'harness@cinema.test', N'Harness User', 0x01, 0x01, 1, @UserType, @Member, 100, @now);

INSERT INTO [AgeRestriction] ([Id],[Code],[Description],[MinAge],[CreationTime]) VALUES (@Age, N'P', N'All', 0, @now);
INSERT INTO [Movie] ([Id],[Slug],[Title],[Description],[Duration],[ReleaseDate],[IsActive],[AgeRestrictionId],[CreationTime])
VALUES (@Movie, N'harness-movie', N'Harness Movie', N'x', 100, '2026-01-01', 1, @Age, @now);
INSERT INTO [ShowTime] ([Id],[MovieId],[StartTime],[EndTime],[ProjectionForm],[ShowTimeType],[IsActive],[CreationTime])
VALUES (@ShowTime, @Movie, DATEADD(DAY,1,@now), DATEADD(HOUR,2,DATEADD(DAY,1,@now)), 0, 0, 1, @now);
INSERT INTO [RoomType] ([Id],[TheaterId],[Name],[CreationTime]) VALUES (@RoomType, @Theater, N'Standard', @now), ('10000000-0000-0000-0000-000000000017', @Theater, N'Premium', @now);
INSERT INTO [Room] ([Id],[Name],[TheaterId],[RoomTypeId],[TotalRows],[TotalColumns],[Status],[CreationTime])
VALUES (@Room, N'R1', @Theater, @RoomType, 1, 5, 0, @now);
INSERT INTO [ShowTimeRoom] ([ShowTimeId],[RoomId],[BasePrice]) VALUES (@ShowTime, @Room, 0);

-- five seats: gift x2 (1,2), child x2 (3,4), unrelated x1 (5)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
VALUES ('30000000-0000-0000-0000-000000000001', @Room, N'A', 1, 1, @now),
       ('30000000-0000-0000-0000-000000000002', @Room, N'A', 2, 1, @now),
       ('30000000-0000-0000-0000-000000000003', @Room, N'A', 3, 1, @now),
       ('30000000-0000-0000-0000-000000000004', @Room, N'A', 4, 1, @now),
       ('30000000-0000-0000-0000-000000000005', @Room, N'A', 5, 1, @now);

INSERT INTO [SeatType] ([Id],[TheaterId],[Kind],[Name],[Color],[CreationTime]) VALUES (@SeatType, @Theater, 0, N'Standard', N'#fff', @now);
INSERT INTO [PatronCategory] ([Id],[TheaterId],[SeatTypeId],[Name],[Price],[CreationTime])
VALUES (@ChildCat, @Theater, @SeatType, @ChildName, 40000, @now),
       (@AdultCat, @Theater, @SeatType, N'Adult', 80000, @now);
INSERT INTO [RoomTypePatronCategoryPrice] ([Id],[RoomTypeId],[PatronCategoryId],[Price],[CreationTime])
VALUES ('40000000-0000-0000-0000-000000000001', @RoomType, @ChildCat, 35000, @now),
       ('40000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000017', @ChildCat, 36000, @now),
       ('40000000-0000-0000-0000-000000000003', @RoomType, @AdultCat, 85000, @now);

INSERT INTO [FoodAndDrink] ([Id],[TheaterId],[Name],[Price],[IsAvailable],[CreationTime]) VALUES (@Food, @Theater, N'Popcorn', 50000, 1, @now);
INSERT INTO [GiftCard] ([Id],[Code],[InitialBalance],[Balance],[CreationTime]) VALUES (@GiftCard, N'GC-HARNESS', 100000, 40000, @now);
INSERT INTO [PromotionProgram] ([Id],[Title],[StartDate],[EndDate],[StacksWithMembershipDiscount],[CreationTime])
VALUES (@Promo, N'Harness Promo', @now, DATEADD(DAY,30,@now), 1, @now);

-- Invoices (Status 0 Pending, 1 Paid)
INSERT INTO [Invoice] ([Id],[Code],[UserId],[TotalAmount],[DiscountAmount],[FinalAmount],[Status],[PointsRedeemed],[GiftCardId],[GiftCardAmount],[CreationTime])
VALUES (@InvGift,        N'INV-GIFT',     @User, 160000, 0, 110000, 1,  0, @GiftCard, 50000, @now),
       (@InvGiftPending, N'INV-GIFTPEND', @User, 100000, 0,  60000, 0, 30, @GiftCard, 10000, @now),
       (@InvChild,       N'INV-CHILD',    @User,  80000, 0,  80000, 1,  0, NULL,          0, @now),
       (@InvOther,       N'INV-OTHER',    @User,  80000, 0,  80000, 1,  0, NULL,          0, @now);
INSERT INTO [Invoice] ([Id],[Code],[UserId],[TotalAmount],[DiscountAmount],[FinalAmount],[Status],[PointsRedeemed],[GiftCardAmount],[ExchangedFromInvoiceId],[CreationTime])
VALUES (@InvSurvivor, N'INV-SURVIVOR', @User, 80000, 0, 80000, 1, 0, 0, @InvChild, @now);

INSERT INTO [InvoiceTicket] ([InvoiceId],[ShowTimeId],[RoomId],[SeatId],[Price],[PatronCategoryId],[PatronCategoryName],[IsUsed])
VALUES (@InvGift,  @ShowTime, @Room, '30000000-0000-0000-0000-000000000001', 80000, @AdultCat, N'Adult', 0),
       (@InvGift,  @ShowTime, @Room, '30000000-0000-0000-0000-000000000002', 80000, @AdultCat, N'Adult', 0),
       (@InvChild, @ShowTime, @Room, '30000000-0000-0000-0000-000000000003', 40000, @ChildCat, @ChildName, 0),
       (@InvChild, @ShowTime, @Room, '30000000-0000-0000-0000-000000000004', 40000, @ChildCat, @ChildName, 0),
       (@InvOther, @ShowTime, @Room, '30000000-0000-0000-0000-000000000005', 80000, @AdultCat, N'Adult', 0);

-- InvoicePayment.Method: 3 = GiftCard, 5 = Online
INSERT INTO [InvoicePayment] ([Id],[InvoiceId],[Method],[Amount],[CreationTime])
VALUES ('50000000-0000-0000-0000-000000000001', @InvGift,  3, 50000, @now),
       ('50000000-0000-0000-0000-000000000002', @InvGift,  5, 60000, @now),
       ('50000000-0000-0000-0000-000000000003', @InvChild, 5, 80000, @now),
       ('50000000-0000-0000-0000-000000000004', @InvOther, 5, 80000, @now);
INSERT INTO [InvoiceFoodAndDrink] ([InvoiceId],[FoodAndDrinkId],[Quantity],[UnitPrice],[TotalPrice])
VALUES (@InvGift, @Food, 1, 50000, 50000), (@InvChild, @Food, 1, 50000, 50000), (@InvOther, @Food, 1, 50000, 50000);
-- StockMovement.InvoiceId has no FK; Type 0 / Quantity 1 are arbitrary but valid ints.
INSERT INTO [StockMovement] ([Id],[FoodAndDrinkId],[TheaterId],[Type],[Quantity],[InvoiceId],[CreationTime])
VALUES ('60000000-0000-0000-0000-000000000001', @Food, @Theater, 0, 1, @InvGift,  @now),
       ('60000000-0000-0000-0000-000000000002', @Food, @Theater, 0, 1, @InvChild, @now),
       ('60000000-0000-0000-0000-000000000003', @Food, @Theater, 0, 1, @InvOther, @now);
PRINT 'seed: old-schema rows inserted';
