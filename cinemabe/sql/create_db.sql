-- ============================================================
-- create_db.sql  --  Fresh database creation
-- Run this on a new SQL Server instance to set up Cinema DB
-- Tables are ordered so every FK references a table already created above it
-- (no deferred ALTER TABLE block needed).
-- ============================================================

-- ── Drop existing tables (idempotent re-run) ──────────────────────────────────
-- Reverse of the creation order below, so every table is dropped before any
-- table it references (FK target) — otherwise SQL Server refuses the drop.
IF OBJECT_ID('dbo.__EFMigrationsHistory', 'U') IS NOT NULL
DROP TABLE dbo.__EFMigrationsHistory;
IF OBJECT_ID('dbo.StaffTask', 'U') IS NOT NULL
DROP TABLE dbo.StaffTask;
IF OBJECT_ID('dbo.TimeClockEntry', 'U') IS NOT NULL
DROP TABLE dbo.TimeClockEntry;
IF OBJECT_ID('dbo.StaffShift', 'U') IS NOT NULL
DROP TABLE dbo.StaffShift;
IF OBJECT_ID('dbo.ChecklistRunItem', 'U') IS NOT NULL
DROP TABLE dbo.ChecklistRunItem;
IF OBJECT_ID('dbo.ChecklistRun', 'U') IS NOT NULL
DROP TABLE dbo.ChecklistRun;
IF OBJECT_ID('dbo.ChecklistTemplateItem', 'U') IS NOT NULL
DROP TABLE dbo.ChecklistTemplateItem;
IF OBJECT_ID('dbo.ChecklistTemplate', 'U') IS NOT NULL
DROP TABLE dbo.ChecklistTemplate;
IF OBJECT_ID('dbo.Incident', 'U') IS NOT NULL
DROP TABLE dbo.Incident;
-- P7 customer service
IF OBJECT_ID('dbo.Complaint', 'U') IS NOT NULL
DROP TABLE dbo.Complaint;
-- P9 reporting
IF OBJECT_ID('dbo.UserTheater', 'U') IS NOT NULL
DROP TABLE dbo.UserTheater;
-- P4 box office
IF OBJECT_ID('dbo.CashMovement', 'U') IS NOT NULL
DROP TABLE dbo.CashMovement;
IF OBJECT_ID('dbo.CashDrawerSession', 'U') IS NOT NULL
DROP TABLE dbo.CashDrawerSession;
IF OBJECT_ID('dbo.InvoicePayment', 'U') IS NOT NULL
DROP TABLE dbo.InvoicePayment;
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL
DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.StoragePlanItem', 'U') IS NOT NULL
DROP TABLE dbo.StoragePlanItem;
IF OBJECT_ID('dbo.StoragePlan', 'U') IS NOT NULL
DROP TABLE dbo.StoragePlan;
IF OBJECT_ID('dbo.StockMovement', 'U') IS NOT NULL
DROP TABLE dbo.StockMovement;
IF OBJECT_ID('dbo.ComboItem', 'U') IS NOT NULL
DROP TABLE dbo.ComboItem;
IF OBJECT_ID('dbo.GiftCard', 'U') IS NOT NULL
DROP TABLE dbo.GiftCard;
IF OBJECT_ID('dbo.ReminderLog', 'U') IS NOT NULL
DROP TABLE dbo.ReminderLog;
IF OBJECT_ID('dbo.RoomTypePatronCategoryPrice', 'U') IS NOT NULL
DROP TABLE dbo.RoomTypePatronCategoryPrice;
IF OBJECT_ID('dbo.TicketPrice', 'U') IS NOT NULL
DROP TABLE dbo.TicketPrice;
IF OBJECT_ID('dbo.TimeSlot', 'U') IS NOT NULL
DROP TABLE dbo.TimeSlot;
IF OBJECT_ID('dbo.InvoiceTicket', 'U') IS NOT NULL
DROP TABLE dbo.InvoiceTicket;
IF OBJECT_ID('dbo.InvoiceFoodAndDrink', 'U') IS NOT NULL
DROP TABLE dbo.InvoiceFoodAndDrink;
IF OBJECT_ID('dbo.ShowTimeRoom', 'U') IS NOT NULL
DROP TABLE dbo.ShowTimeRoom;
IF OBJECT_ID('dbo.Invoice', 'U') IS NOT NULL
DROP TABLE dbo.Invoice;
IF OBJECT_ID('dbo.Evaluation', 'U') IS NOT NULL
DROP TABLE dbo.Evaluation;
IF OBJECT_ID('dbo.Comment', 'U') IS NOT NULL
DROP TABLE dbo.Comment;
IF OBJECT_ID('dbo.Seat', 'U') IS NOT NULL
DROP TABLE dbo.Seat;
IF OBJECT_ID('dbo.ShowTime', 'U') IS NOT NULL
DROP TABLE dbo.ShowTime;
IF OBJECT_ID('dbo.MovieTypeDetail', 'U') IS NOT NULL
DROP TABLE dbo.MovieTypeDetail;
IF OBJECT_ID('dbo.User', 'U') IS NOT NULL
DROP TABLE dbo.[User];
IF OBJECT_ID('dbo.Room', 'U') IS NOT NULL
DROP TABLE dbo.Room;
IF OBJECT_ID('dbo.DiscountTheater', 'U') IS NOT NULL
DROP TABLE dbo.DiscountTheater;
IF OBJECT_ID('dbo.Discount', 'U') IS NOT NULL
DROP TABLE dbo.Discount;
IF OBJECT_ID('dbo.Movie', 'U') IS NOT NULL
DROP TABLE dbo.Movie;
IF OBJECT_ID('dbo.FoodAndDrink', 'U') IS NOT NULL
DROP TABLE dbo.FoodAndDrink;
IF OBJECT_ID('dbo.PatronCategory', 'U') IS NOT NULL
DROP TABLE dbo.PatronCategory;
IF OBJECT_ID('dbo.SeatType', 'U') IS NOT NULL
DROP TABLE dbo.SeatType;
IF OBJECT_ID('dbo.RoomType', 'U') IS NOT NULL
DROP TABLE dbo.RoomType;
IF OBJECT_ID('dbo.Theater', 'U') IS NOT NULL
DROP TABLE dbo.Theater;
IF OBJECT_ID('dbo.UserType', 'U') IS NOT NULL
DROP TABLE dbo.UserType;
IF OBJECT_ID('dbo.News', 'U') IS NOT NULL
DROP TABLE dbo.News;
IF OBJECT_ID('dbo.MovieType', 'U') IS NOT NULL
DROP TABLE dbo.MovieType;
IF OBJECT_ID('dbo.MemberShip', 'U') IS NOT NULL
DROP TABLE dbo.MemberShip;
IF OBJECT_ID('dbo.Holiday', 'U') IS NOT NULL
DROP TABLE dbo.Holiday;
IF OBJECT_ID('dbo.DiscountType', 'U') IS NOT NULL
DROP TABLE dbo.DiscountType;
IF OBJECT_ID('dbo.AgeRestriction', 'U') IS NOT NULL
DROP TABLE dbo.AgeRestriction;

CREATE TABLE [AgeRestriction] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Code] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [MinAge] int NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_AgeRestriction] PRIMARY KEY ([Id])
);

CREATE TABLE [DiscountType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_DiscountType] PRIMARY KEY ([Id])
);

CREATE TABLE [Holiday] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [Date] date NOT NULL,
    [PriceMultiplier] float NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Holiday] PRIMARY KEY ([Id])
);

CREATE TABLE [MemberShip] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [MinPoints] int NOT NULL,
    [MaxPoints] int NOT NULL,
    [DiscountPercent] float NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_MemberShip] PRIMARY KEY ([Id])
);

CREATE TABLE [MovieType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_MovieType] PRIMARY KEY ([Id])
);

CREATE TABLE [News] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Title] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [ThumbnailUrl] nvarchar(max) NULL,
    [Author] nvarchar(max) NULL,
    [IsPublished] bit NOT NULL,
    [PublishedAt] datetime NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_News] PRIMARY KEY ([Id])
);

CREATE TABLE [UserType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_UserType] PRIMARY KEY ([Id])
);

CREATE TABLE [Theater] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(200) NOT NULL,
    [Address] nvarchar(500) NOT NULL,
    [City] nvarchar(100) NOT NULL,
    [Phone] nvarchar(max) NULL,
    [Email] nvarchar(max) NULL,
    [ImageUrl] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Theater] PRIMARY KEY ([Id])
);

CREATE TABLE [RoomType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(max) NULL,
    -- Projection capability of the class, and the flat per-ticket amount a 3D screening adds on
    -- top of its base price. Not derivable from [Name]: a premium class such as N'Lagom' is an
    -- interior brand that may or may not have a 3D projector.
    [SupportsThreeD] bit NOT NULL DEFAULT 0,
    [ThreeDSurcharge] float NOT NULL DEFAULT 0,
    -- Minutes of exclusive room time reserved between the end of one screening and the start of
    -- the next, for audience seating and cleanup. 0 = no buffer required.
    [TurnoverBufferMinutes] int NOT NULL DEFAULT 0,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_RoomType] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoomType_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
);

-- Exactly 2 rows per theater (Standard, Double), seeded on theater creation. Kind is the machine-
-- readable identity (never match on Name, which is display text an admin can rename); no pricing
-- lives here anymore — PatronCategory owns the per-seat-kind price.
CREATE TABLE [SeatType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL DEFAULT 0,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Color] nvarchar(max) NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_SeatType] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SeatType_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
);

-- One row per (theater, logical category name, seat kind) — e.g. Adult/Standard and Adult/Double are
-- two separate rows sharing the name "Adult". Price is absolute VND, independently configured per row
-- (a Double row is NEVER computed as 2x Standard). Omitting a seat kind for a category means that
-- category cannot book that kind at all — this is the entire eligibility rule, there is no separate
-- allow-list table.
CREATE TABLE [PatronCategory] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [SeatTypeId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Price] float NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT 1,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_PatronCategory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PatronCategory_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PatronCategory_SeatType_SeatTypeId] FOREIGN KEY ([SeatTypeId]) REFERENCES [SeatType] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [FoodAndDrink] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Price] float NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [IsAvailable] bit NOT NULL,
    [TrackInventory] bit NOT NULL DEFAULT 0,
    [QuantityOnHand] int NOT NULL DEFAULT 0,
    [LowStockThreshold] int NOT NULL DEFAULT 0,
    [TargetStockLevel] int NOT NULL DEFAULT 0,
    [IsCombo] bit NOT NULL DEFAULT 0,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_FoodAndDrink] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FoodAndDrink_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_FoodAndDrink_QuantityOnHand] CHECK ([QuantityOnHand] >= 0)
);

CREATE TABLE [Movie] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Duration] int NOT NULL,
    [ReleaseDate] date NOT NULL,
    [EndDate] date NULL,
    [PosterUrl] nvarchar(max) NULL,
    [TrailerUrl] nvarchar(max) NULL,
    [Director] nvarchar(200) NULL,
    [Cast] nvarchar(1000) NULL,
    [Language] nvarchar(100) NULL,
    [Subtitle] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [AgeRestrictionId] uniqueidentifier NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Movie] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Movie_AgeRestriction_AgeRestrictionId] FOREIGN KEY ([AgeRestrictionId]) REFERENCES [AgeRestriction] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Discount] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Code] nvarchar(50) NULL,
    [Description] nvarchar(max) NULL,
    [Percent] float NOT NULL,
    [MaxDiscountAmount] float NULL,
    [DiscountTypeId] uniqueidentifier NOT NULL,
    [StartDate] datetime NOT NULL,
    [EndDate] datetime NOT NULL,
    [MaxUsage] int NULL,
    [UsedCount] int NOT NULL,
    [IsActive] bit NOT NULL,
    [AutoApply] bit NOT NULL DEFAULT 0,
    [ApplyToAllTheaters] bit NOT NULL DEFAULT 1,
    [MovieId] uniqueidentifier NULL,
    [DaysOfWeekMask] int NULL,
    [StartTimeOfDay] time NULL,
    [EndTimeOfDay] time NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Discount] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Discount_DiscountType_DiscountTypeId] FOREIGN KEY ([DiscountTypeId]) REFERENCES [DiscountType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Discount_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE SET NULL
);

-- Theaters a promotion is limited to (only when [Discount].[ApplyToAllTheaters] = 0).
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

CREATE TABLE [Room] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(100) NOT NULL,
    [TheaterId] uniqueidentifier NOT NULL,
    [RoomTypeId] uniqueidentifier NOT NULL,
    [TotalRows] int NOT NULL,
    [TotalColumns] int NOT NULL,
    [Status] int NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Room] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Room_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Room_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [User] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Phone] nvarchar(20) NOT NULL,
    [Email] nvarchar(200) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Avatar] nvarchar(max) NULL,
    [PasswordHash] varbinary(max) NOT NULL,
    [PasswordSalt] varbinary(max) NOT NULL,
    [Status] int NOT NULL,
    [UserTypeId] uniqueidentifier NOT NULL,
    [TheaterId] uniqueidentifier NULL,
    [MemberShipId] uniqueidentifier NULL,
    [Points] int NOT NULL,
    [NotifyBookingEmails] bit NOT NULL DEFAULT 1,
    [NotifyPromotionEmails] bit NOT NULL DEFAULT 1,
    [NotifyReminderEmails] bit NOT NULL DEFAULT 1,
    [PasswordResetTokenHash] nvarchar(max) NULL,
    [PasswordResetExpiresAt] datetime NULL,
    [FailedLoginCount] int NOT NULL DEFAULT 0,
    [LockoutEndUtc] datetime NULL,
    [EmailConfirmed] bit NOT NULL DEFAULT 0,
    [EmailVerificationTokenHash] nvarchar(max) NULL,
    [EmailVerificationExpiresAt] datetime NULL,
    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
    [TwoFactorCodeHash] nvarchar(max) NULL,
    [TwoFactorCodeExpiresAt] datetime NULL,
    [OverridePinHash] varbinary(64) NULL,
    [OverridePinSalt] varbinary(128) NULL,
    [OverridePinFailedCount] int NOT NULL DEFAULT 0,
    [OverridePinLockoutEndUtc] datetime NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_User] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_User_MemberShip_MemberShipId] FOREIGN KEY ([MemberShipId]) REFERENCES [MemberShip] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_User_UserType_UserTypeId] FOREIGN KEY ([UserTypeId]) REFERENCES [UserType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_User_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [MovieTypeDetail] (
    [MovieId] uniqueidentifier NOT NULL,
    [MovieTypeId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MovieTypeDetail] PRIMARY KEY ([MovieId], [MovieTypeId]),
    CONSTRAINT [FK_MovieTypeDetail_MovieType_MovieTypeId] FOREIGN KEY ([MovieTypeId]) REFERENCES [MovieType] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MovieTypeDetail_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ShowTime] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [MovieId] uniqueidentifier NOT NULL,
    [StartTime] datetime NOT NULL,
    [EndTime] datetime NOT NULL,
    [ProjectionForm] int NOT NULL,
    [ShowTimeType] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_ShowTime] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ShowTime_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE CASCADE
);

-- A seat has no stored kind of its own: Standard vs Double is derived from SeatGroupId being set
-- (a non-null group id always links exactly 2 seats). This avoids two places that could disagree
-- about a seat's kind.
CREATE TABLE [Seat] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [RoomId] uniqueidentifier NOT NULL,
    [RowName] nvarchar(5) NOT NULL,
    [ColIndex] int NOT NULL,
    [IsActive] bit NOT NULL,
    [SeatGroupId] uniqueidentifier NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Seat] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Seat_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Comment] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [MovieId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [IsApproved] bit NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Comment] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Comment_Comment_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Comment] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Comment_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Comment_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Evaluation] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [MovieId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Score] int NOT NULL,
    [Review] nvarchar(1000) NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Evaluation] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Evaluation_Movie_MovieId] FOREIGN KEY ([MovieId]) REFERENCES [Movie] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Evaluation_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Invoice] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Code] nvarchar(50) NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TotalAmount] float NOT NULL,
    [DiscountAmount] float NOT NULL,
    [FinalAmount] float NOT NULL,
    [Status] int NOT NULL,
    [PaymentMethod] nvarchar(max) NULL,
    [PaymentReference] nvarchar(max) NULL,
    [PaidAt] datetime NULL,
    [RefundedAt] datetime NULL,
    [PointsRedeemed] int NOT NULL DEFAULT 0,
    [GiftCardId] uniqueidentifier NULL,
    [GiftCardAmount] float NOT NULL DEFAULT 0,
    [DiscountId] uniqueidentifier NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Invoice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invoice_Discount_DiscountId] FOREIGN KEY ([DiscountId]) REFERENCES [Discount] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Invoice_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ShowTimeRoom] (
    [ShowTimeId] uniqueidentifier NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    -- Flat per-showtime surcharge added to every ticket (0 = none) — a manual admin lever per
    -- title/showtime (e.g. a premium for a new release), NOT the seat's base price; PatronCategory
    -- owns that.
    [BasePrice] float NOT NULL,
    CONSTRAINT [PK_ShowTimeRoom] PRIMARY KEY ([ShowTimeId], [RoomId]),
    CONSTRAINT [FK_ShowTimeRoom_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShowTimeRoom_ShowTime_ShowTimeId] FOREIGN KEY ([ShowTimeId]) REFERENCES [ShowTime] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [InvoiceFoodAndDrink] (
    [InvoiceId] uniqueidentifier NOT NULL,
    [FoodAndDrinkId] uniqueidentifier NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] float NOT NULL,
    [TotalPrice] float NOT NULL,
    CONSTRAINT [PK_InvoiceFoodAndDrink] PRIMARY KEY ([InvoiceId], [FoodAndDrinkId]),
    CONSTRAINT [FK_InvoiceFoodAndDrink_FoodAndDrink_FoodAndDrinkId] FOREIGN KEY ([FoodAndDrinkId]) REFERENCES [FoodAndDrink] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InvoiceFoodAndDrink_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([Id]) ON DELETE CASCADE
);

-- Inventory: combo recipes, stock ledger, storage (restock) plans
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

CREATE TABLE [InvoiceTicket] (
    [InvoiceId] uniqueidentifier NOT NULL,
    [ShowTimeId] uniqueidentifier NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    [SeatId] uniqueidentifier NOT NULL,
    [Price] float NOT NULL,
    -- Snapshot of the patron category applied at booking time (no FK, like Invoice.GiftCardId) so
    -- reprints/gate check-in/reports stay truthful even if the category is later renamed or deleted.
    [PatronCategoryId] uniqueidentifier NULL,
    [PatronCategoryName] nvarchar(100) NULL,
    [PatronDiscountPercent] float NOT NULL DEFAULT 0,
    [QrCode] nvarchar(max) NULL,
    [IsUsed] bit NOT NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    -- P3 gate: stamped atomically with IsUsed when a ticket is admitted at the gate.
    [UsedAt] datetime NULL,
    [UsedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_InvoiceTicket] PRIMARY KEY ([InvoiceId], [ShowTimeId], [RoomId], [SeatId]),
    CONSTRAINT [FK_InvoiceTicket_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_InvoiceTicket_Seat_SeatId] FOREIGN KEY ([SeatId]) REFERENCES [Seat] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_InvoiceTicket_ShowTimeRoom_ShowTimeId_RoomId] FOREIGN KEY ([ShowTimeId], [RoomId]) REFERENCES [ShowTimeRoom] ([ShowTimeId], [RoomId]) ON DELETE NO ACTION
);

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

-- A time-of-day/holiday pricing factor for (theater, room type, time slot, holiday?). Multiplies the
-- resolved PatronCategory price — no seat-kind dimension here anymore, since PatronCategory now owns
-- the per-seat-kind price absolutely.
CREATE TABLE [TicketPrice] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [RoomTypeId] uniqueidentifier NOT NULL,
    [TimeSlotId] uniqueidentifier NOT NULL,
    [IsHoliday] bit NOT NULL,
    [PriceMultiplier] float NOT NULL DEFAULT 1,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_TicketPrice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TicketPrice_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TicketPrice_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TicketPrice_TimeSlot_TimeSlotId] FOREIGN KEY ([TimeSlotId]) REFERENCES [TimeSlot] ([Id]) ON DELETE NO ACTION
);

-- A RoomType's patron-category allow-list: a RoomType offers EXACTLY the categories that have a row
-- here, at that row's Price. Zero rows means the RoomType offers nothing — there is no "unrestricted,
-- falls back to the theater-wide default" state. (e.g. Room Type 1 = Adult Standard + Child Standard;
-- Room Type 2 = Child Double + Child Standard + Senior Standard.) RoomTypeManager.CreateAsync seeds a
-- new RoomType with one row per active theater-wide PatronCategory so it's bookable immediately; the
-- admin then removes the ones that shouldn't be offered there.
CREATE TABLE [RoomTypePatronCategoryPrice] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [RoomTypeId] uniqueidentifier NOT NULL,
    [PatronCategoryId] uniqueidentifier NOT NULL,
    [Price] float NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_RoomTypePatronCategoryPrice] PRIMARY KEY ([Id]),
    -- Both RoomType and PatronCategory already cascade from Theater, so only one leg here may cascade
    -- (SQL Server rejects multiple cascade paths). PatronCategory cascades; RoomType deletes clean up
    -- override rows explicitly (RoomTypeManager.DeleteAsync).
    CONSTRAINT [FK_RoomTypePatronCategoryPrice_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RoomTypePatronCategoryPrice_PatronCategory_PatronCategoryId] FOREIGN KEY ([PatronCategoryId]) REFERENCES [PatronCategory] ([Id]) ON DELETE CASCADE
);

-- Showtime reminders already sent (persists dedup across restarts). No FKs — a purely transient log.
CREATE TABLE [ReminderLog] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [UserId] uniqueidentifier NOT NULL,
    [ShowTimeId] uniqueidentifier NOT NULL,
    [SentAt] datetime NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_ReminderLog] PRIMARY KEY ([Id])
);

-- Stored-value gift cards / vouchers.
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

-- Indexes
CREATE INDEX [IX_Comment_MovieId] ON [Comment] ([MovieId]);
CREATE INDEX [IX_Comment_ParentId] ON [Comment] ([ParentId]);
CREATE INDEX [IX_Comment_UserId] ON [Comment] ([UserId]);
CREATE UNIQUE INDEX [IX_Discount_Code] ON [Discount] ([Code]) WHERE [Code] IS NOT NULL;
CREATE INDEX [IX_Discount_DiscountTypeId] ON [Discount] ([DiscountTypeId]);
CREATE INDEX [IX_Discount_MovieId] ON [Discount] ([MovieId]);
CREATE UNIQUE INDEX [IX_DiscountTheater_DiscountId_TheaterId] ON [DiscountTheater] ([DiscountId], [TheaterId]);
CREATE INDEX [IX_DiscountTheater_TheaterId] ON [DiscountTheater] ([TheaterId]);
CREATE UNIQUE INDEX [IX_Evaluation_MovieId_UserId] ON [Evaluation] ([MovieId], [UserId]);
CREATE INDEX [IX_Evaluation_UserId] ON [Evaluation] ([UserId]);
CREATE INDEX [IX_FoodAndDrink_TheaterId] ON [FoodAndDrink] ([TheaterId]);
CREATE INDEX [IX_InvoiceFoodAndDrink_FoodAndDrinkId] ON [InvoiceFoodAndDrink] ([FoodAndDrinkId]);
CREATE UNIQUE INDEX [IX_Invoice_Code] ON [Invoice] ([Code]);
CREATE INDEX [IX_Invoice_DiscountId] ON [Invoice] ([DiscountId]);
CREATE INDEX [IX_Invoice_UserId] ON [Invoice] ([UserId]);
CREATE INDEX [IX_InvoiceTicket_SeatId] ON [InvoiceTicket] ([SeatId]);
CREATE INDEX [IX_InvoiceTicket_ShowTimeId_RoomId] ON [InvoiceTicket] ([ShowTimeId], [RoomId]);
-- Cross-instance double-booking guard: at most one active ticket per (showtime, room, seat).
CREATE UNIQUE INDEX [IX_InvoiceTicket_ActiveSeat] ON [InvoiceTicket] ([ShowTimeId], [RoomId], [SeatId]) WHERE [IsActive] = 1;
CREATE INDEX [IX_Movie_AgeRestrictionId] ON [Movie] ([AgeRestrictionId]);
CREATE INDEX [IX_MovieTypeDetail_MovieTypeId] ON [MovieTypeDetail] ([MovieTypeId]);
CREATE INDEX [IX_Room_TheaterId] ON [Room] ([TheaterId]);
CREATE UNIQUE INDEX [IX_Seat_RoomId_RowName_ColIndex] ON [Seat] ([RoomId], [RowName], [ColIndex]);
CREATE INDEX [IX_Seat_SeatGroupId] ON [Seat] ([SeatGroupId]);
CREATE UNIQUE INDEX [IX_SeatType_TheaterId_Kind] ON [SeatType] ([TheaterId], [Kind]);
CREATE INDEX [IX_PatronCategory_TheaterId] ON [PatronCategory] ([TheaterId]);
CREATE INDEX [IX_PatronCategory_SeatTypeId] ON [PatronCategory] ([SeatTypeId]);
CREATE UNIQUE INDEX [IX_PatronCategory_TheaterId_Name_SeatTypeId] ON [PatronCategory] ([TheaterId], [Name], [SeatTypeId]);
CREATE INDEX [IX_RoomTypePatronCategoryPrice_PatronCategoryId] ON [RoomTypePatronCategoryPrice] ([PatronCategoryId]);
CREATE UNIQUE INDEX [IX_RoomTypePatronCategoryPrice_RoomTypeId_PatronCategoryId] ON [RoomTypePatronCategoryPrice] ([RoomTypeId], [PatronCategoryId]);
CREATE INDEX [IX_TimeSlot_TheaterId] ON [TimeSlot] ([TheaterId]);
CREATE INDEX [IX_TicketPrice_RoomTypeId] ON [TicketPrice] ([RoomTypeId]);
CREATE INDEX [IX_TicketPrice_TimeSlotId] ON [TicketPrice] ([TimeSlotId]);
CREATE UNIQUE INDEX [IX_TicketPrice_TheaterId_RoomTypeId_TimeSlotId_IsHoliday] ON [TicketPrice] ([TheaterId], [RoomTypeId], [TimeSlotId], [IsHoliday]);
CREATE INDEX [IX_RoomType_TheaterId] ON [RoomType] ([TheaterId]);
CREATE INDEX [IX_Room_RoomTypeId] ON [Room] ([RoomTypeId]);
CREATE INDEX [IX_ShowTimeRoom_RoomId] ON [ShowTimeRoom] ([RoomId]);
CREATE INDEX [IX_ShowTime_MovieId] ON [ShowTime] ([MovieId]);
CREATE UNIQUE INDEX [IX_User_Email] ON [User] ([Email]);
CREATE INDEX [IX_User_MemberShipId] ON [User] ([MemberShipId]);
CREATE UNIQUE INDEX [IX_User_Phone] ON [User] ([Phone]);
CREATE INDEX [IX_User_UserTypeId] ON [User] ([UserTypeId]);
CREATE INDEX [IX_User_TheaterId] ON [User] ([TheaterId]);
CREATE UNIQUE INDEX [IX_ReminderLog_UserId_ShowTimeId] ON [ReminderLog] ([UserId], [ShowTimeId]);
CREATE UNIQUE INDEX [IX_GiftCard_Code] ON [GiftCard] ([Code]);

-- Insert-only audit trail of sensitive staff actions (no FKs, so it survives deletions).
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

-- ── P8a incidents ────────────────────────────────────────────────────────────
-- Staff incident reports (seat/room blocks reference them). No FKs, like AuditLog: the history survives deletions.
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
-- ── end P8a incidents ────────────────────────────────────────────────────────

-- ── P8b checklists ───────────────────────────────────────────────────────────
-- Reusable per-theater templates; a run (a showtime's copy of the template) is created lazily on first open.
-- Runs keep no FK to the template or showtime, so template edits/deletions never rewrite history.
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
-- ── end P8b checklists ───────────────────────────────────────────────────────

-- ── P8c workforce ────────────────────────────────────────────────────────────
-- Rosters, time clock and staff tasks. No FKs (like AuditLog/Incident): history survives user/theater deletions.
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
-- ── end P8c workforce ────────────────────────────────────────────────────────

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

-- ===== P9 reporting =====
-- Theater assignments of a RegionalManager (decision D12). Composite PK; NO ACTION FKs (users are soft-deleted,
-- a theater with assignments cannot be removed by surprise).
IF OBJECT_ID('dbo.UserTheater', 'U') IS NULL
BEGIN
    CREATE TABLE [UserTheater] (
        [UserId] uniqueidentifier NOT NULL,
        [TheaterId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserTheater] PRIMARY KEY ([UserId], [TheaterId]),
        CONSTRAINT [FK_UserTheater_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserTheater_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_UserTheater_TheaterId] ON [UserTheater] ([TheaterId]);
END
-- ===== end P9 reporting =====

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
-- ── end P7 customer service ──────────────────────────────────────────────────

-- ============================================================
-- EF Core migrations baseline
-- ============================================================
-- The schema above is intentionally maintained by hand (see CLAUDE.md: schema changes go into
-- create_db.sql/upgrade_db.sql, never `dotnet ef migrations add`). Stamping the history table here
-- marks this fresh database as already migrated, so `dotnet ef database update` applies only later
-- migrations instead of trying to re-create everything.
--
-- Keep this row in sync with the InitialBaseline migration id if it is ever regenerated.

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
