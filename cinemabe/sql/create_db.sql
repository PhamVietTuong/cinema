-- ============================================================
-- create_db.sql  --  Fresh database creation
-- Run this on a new SQL Server instance to set up Cinema DB
-- ============================================================

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

CREATE TABLE [FoodAndDrink] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Price] float NOT NULL,
    [ImageUrl] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [IsAvailable] bit NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_FoodAndDrink] PRIMARY KEY ([Id])
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

CREATE TABLE [SeatType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Color] nvarchar(max) NOT NULL,
    [PriceMultiplier] float NOT NULL DEFAULT 1,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_SeatType] PRIMARY KEY ([Id])
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
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_RoomType] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RoomType_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserType] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [Name] nvarchar(max) NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_UserType] PRIMARY KEY ([Id])
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

CREATE TABLE [Seat] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [RoomId] uniqueidentifier NOT NULL,
    [RowName] nvarchar(5) NOT NULL,
    [ColIndex] int NOT NULL,
    [SeatTypeId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [SeatGroupId] uniqueidentifier NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_Seat] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Seat_Room_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Room] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Seat_SeatType_SeatTypeId] FOREIGN KEY ([SeatTypeId]) REFERENCES [SeatType] ([Id]) ON DELETE NO ACTION
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

CREATE TABLE [InvoiceTicket] (
    [InvoiceId] uniqueidentifier NOT NULL,
    [ShowTimeId] uniqueidentifier NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    [SeatId] uniqueidentifier NOT NULL,
    [Price] float NOT NULL,
    [QrCode] nvarchar(max) NULL,
    [IsUsed] bit NOT NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
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

CREATE TABLE [TicketPrice] (
    [Id] uniqueidentifier NOT NULL DEFAULT NEWID(),
    [TheaterId] uniqueidentifier NOT NULL,
    [RoomTypeId] uniqueidentifier NOT NULL,
    [SeatTypeId] uniqueidentifier NOT NULL,
    [TimeSlotId] uniqueidentifier NOT NULL,
    [IsHoliday] bit NOT NULL,
    [Price] float NOT NULL,
    [CreationTime] datetime NOT NULL,
    [LastUpdatedTime] datetime NULL,
    CONSTRAINT [PK_TicketPrice] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TicketPrice_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TicketPrice_RoomType_RoomTypeId] FOREIGN KEY ([RoomTypeId]) REFERENCES [RoomType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TicketPrice_SeatType_SeatTypeId] FOREIGN KEY ([SeatTypeId]) REFERENCES [SeatType] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_TicketPrice_TimeSlot_TimeSlotId] FOREIGN KEY ([TimeSlotId]) REFERENCES [TimeSlot] ([Id]) ON DELETE NO ACTION
);

-- Deferred FKs: SeatType and FoodAndDrink are created before Theater, so their
-- theater FK is added here once both tables exist.
ALTER TABLE [SeatType]     ADD CONSTRAINT [FK_SeatType_Theater_TheaterId]     FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE;
ALTER TABLE [FoodAndDrink] ADD CONSTRAINT [FK_FoodAndDrink_Theater_TheaterId] FOREIGN KEY ([TheaterId]) REFERENCES [Theater] ([Id]) ON DELETE CASCADE;

-- Showtime reminders already sent (persists dedup across restarts).
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
CREATE INDEX [IX_Seat_SeatTypeId] ON [Seat] ([SeatTypeId]);
CREATE INDEX [IX_Seat_SeatGroupId] ON [Seat] ([SeatGroupId]);
CREATE INDEX [IX_SeatType_TheaterId] ON [SeatType] ([TheaterId]);
CREATE INDEX [IX_TimeSlot_TheaterId] ON [TimeSlot] ([TheaterId]);
CREATE INDEX [IX_TicketPrice_RoomTypeId] ON [TicketPrice] ([RoomTypeId]);
CREATE INDEX [IX_TicketPrice_SeatTypeId] ON [TicketPrice] ([SeatTypeId]);
CREATE INDEX [IX_TicketPrice_TimeSlotId] ON [TicketPrice] ([TimeSlotId]);
CREATE UNIQUE INDEX [IX_TicketPrice_TheaterId_RoomTypeId_SeatTypeId_TimeSlotId_IsHoliday] ON [TicketPrice] ([TheaterId], [RoomTypeId], [SeatTypeId], [TimeSlotId], [IsHoliday]);
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
