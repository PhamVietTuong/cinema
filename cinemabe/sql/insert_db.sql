-- ============================================================
-- insert_db.sql  --  Seed data (Guid primary keys)
-- Run after create_db.sql to populate with initial/demo data
-- ============================================================

-- ── Age Restrictions ──────────────────────────────────────────────────────────
DECLARE @AgeG   uniqueidentifier = NEWID();
DECLARE @AgePG  uniqueidentifier = NEWID();
DECLARE @AgeT13 uniqueidentifier = NEWID();
DECLARE @AgeT16 uniqueidentifier = NEWID();
DECLARE @AgeT18 uniqueidentifier = NEWID();

INSERT INTO [AgeRestriction] ([Id], [Code], [Description], [MinAge], [CreationTime]) VALUES
(@AgeG,   N'G',   N'General – Suitable for all ages', 0,  GETUTCDATE()),
(@AgePG,  N'PG',  N'Parental Guidance suggested',     8,  GETUTCDATE()),
(@AgeT13, N'T13', N'Not suitable under 13',           13, GETUTCDATE()),
(@AgeT16, N'T16', N'Not suitable under 16',           16, GETUTCDATE()),
(@AgeT18, N'T18', N'Adults only (18+)',               18, GETUTCDATE());

-- ── User Types ────────────────────────────────────────────────────────────────
DECLARE @UserTypeAdmin    uniqueidentifier = NEWID();
DECLARE @UserTypeCustomer uniqueidentifier = NEWID();
DECLARE @UserTypeStaff    uniqueidentifier = NEWID();

INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES
(@UserTypeAdmin,    N'Admin',        GETUTCDATE()),
(@UserTypeCustomer, N'Customer',     GETUTCDATE()),
(@UserTypeStaff,    N'TheaterStaff', GETUTCDATE());

-- ── Membership tiers ─────────────────────────────────────────────────────────
DECLARE @MemberBronze  uniqueidentifier = NEWID();
DECLARE @MemberSilver  uniqueidentifier = NEWID();
DECLARE @MemberGold    uniqueidentifier = NEWID();
DECLARE @MemberDiamond uniqueidentifier = NEWID();

INSERT INTO [MemberShip] ([Id], [Name], [MinPoints], [MaxPoints], [DiscountPercent], [CreationTime]) VALUES
(@MemberBronze,  N'Bronze',  0,     999,   0,  GETUTCDATE()),
(@MemberSilver,  N'Silver',  1000,  4999,  5,  GETUTCDATE()),
(@MemberGold,    N'Gold',    5000,  9999,  10, GETUTCDATE()),
(@MemberDiamond, N'Diamond', 10000, 99999, 15, GETUTCDATE());

-- ── Movie Types ───────────────────────────────────────────────────────────────
DECLARE @MTAction    uniqueidentifier = NEWID();
DECLARE @MTComedy    uniqueidentifier = NEWID();
DECLARE @MTDrama     uniqueidentifier = NEWID();
DECLARE @MTHorror    uniqueidentifier = NEWID();
DECLARE @MTSciFi     uniqueidentifier = NEWID();
DECLARE @MTAnimation uniqueidentifier = NEWID();
DECLARE @MTRomance   uniqueidentifier = NEWID();
DECLARE @MTThriller  uniqueidentifier = NEWID();

INSERT INTO [MovieType] ([Id], [Name], [CreationTime]) VALUES
(@MTAction,    N'Action',    GETUTCDATE()),
(@MTComedy,    N'Comedy',    GETUTCDATE()),
(@MTDrama,     N'Drama',     GETUTCDATE()),
(@MTHorror,    N'Horror',    GETUTCDATE()),
(@MTSciFi,     N'Sci-Fi',    GETUTCDATE()),
(@MTAnimation, N'Animation', GETUTCDATE()),
(@MTRomance,   N'Romance',   GETUTCDATE()),
(@MTThriller,  N'Thriller',  GETUTCDATE());

-- ── Seat Types ────────────────────────────────────────────────────────────────
DECLARE @STStandard uniqueidentifier = NEWID();
DECLARE @STVIP      uniqueidentifier = NEWID();
DECLARE @STCouple   uniqueidentifier = NEWID();

-- Seat types are per-theater now; they are seeded after the theaters exist (below).
-- @STStandard/@STVIP/@STCouple are reused as scratch vars, reassigned per theater
-- right before that theater's seats are generated.

-- ── Discount Types ────────────────────────────────────────────────────────────
DECLARE @DTPromotional uniqueidentifier = NEWID();
DECLARE @DTSeasonal    uniqueidentifier = NEWID();
DECLARE @DTMember      uniqueidentifier = NEWID();

INSERT INTO [DiscountType] ([Id], [Name], [CreationTime]) VALUES
(@DTPromotional, N'Promotional', GETUTCDATE()),
(@DTSeasonal,    N'Seasonal',    GETUTCDATE()),
(@DTMember,      N'Member',      GETUTCDATE());

-- ── Discounts ─────────────────────────────────────────────────────────────────
INSERT INTO [Discount] ([Id], [Code], [Description], [Percent], [MaxDiscountAmount], [DiscountTypeId],
    [StartDate], [EndDate], [MaxUsage], [UsedCount], [IsActive], [CreationTime]) VALUES
(NEWID(), N'WELCOME10', N'10% off for new members',           10, 20000,  @DTPromotional, GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 500,  0, 1, GETUTCDATE()),
(NEWID(), N'SUMMER20',  N'Summer festival – 20% discount',    20, 40000,  @DTSeasonal,    GETUTCDATE(), DATEADD(month,3,GETUTCDATE()), 200, 0, 1, GETUTCDATE()),
(NEWID(), N'STUDENT15', N'15% off with student ID',           15, 30000,  @DTMember,      GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 1000, 0, 1, GETUTCDATE()),
(NEWID(), N'COUPLE30',  N'30% off couple seats on weekends',  30, 60000,  @DTSeasonal,    GETUTCDATE(), DATEADD(month,6,GETUTCDATE()), 100, 0, 1, GETUTCDATE());

-- ── Holidays ──────────────────────────────────────────────────────────────────
INSERT INTO [Holiday] ([Id], [Name], [Date], [PriceMultiplier], [CreationTime]) VALUES
(NEWID(), N'New Year Day',          '2026-01-01', 1.2, GETUTCDATE()),
(NEWID(), N'Vietnamese New Year',   '2026-01-29', 1.5, GETUTCDATE()),
(NEWID(), N'Reunification Day',     '2026-04-30', 1.3, GETUTCDATE()),
(NEWID(), N'International Workers', '2026-05-01', 1.3, GETUTCDATE()),
(NEWID(), N'National Day',          '2026-09-02', 1.3, GETUTCDATE()),
(NEWID(), N'Christmas',             '2026-12-25', 1.2, GETUTCDATE());

-- Food & drinks are per-theater now; they are seeded after the theaters exist (below).

-- ── Theaters ──────────────────────────────────────────────────────────────────
DECLARE @Theater1 uniqueidentifier = NEWID();
DECLARE @Theater2 uniqueidentifier = NEWID();
DECLARE @Theater3 uniqueidentifier = NEWID();

INSERT INTO [Theater] ([Id], [Name], [Address], [City], [Phone], [Email], [IsActive], [Latitude], [Longitude], [CreationTime]) VALUES
(@Theater1, N'Cinema Grand Central',    N'123 Nguyen Hue Boulevard, District 1',          N'Ho Chi Minh City', N'028-3821-1234', N'grandcentral@cinema.vn',    1, 10.7743, 106.7038, GETUTCDATE()),
(@Theater2, N'Cinema Landmark 81',      N'461A Dien Bien Phu Street, Binh Thanh District', N'Ho Chi Minh City', N'028-3512-5678', N'landmark81@cinema.vn',      1, 10.7951, 106.7218, GETUTCDATE()),
(@Theater3, N'Cinema Vincom Royal City',N'72A Nguyen Trai Street, Thanh Xuan District',   N'Hanoi',            N'024-3795-9101', N'royalcity@cinema.vn',        1, 21.0016, 105.8126, GETUTCDATE());

-- ── Room types (per theater) ──────────────────────────────────────────────────
-- Room classes stay at the baseline column set here: insert_db.sql runs before
-- `dotnet ef database update`, so SupportsThreeD / ThreeDSurcharge do not exist yet.
-- The RoomTypeThreeDSupport migration adds those columns and fills them in for these classes.
INSERT INTO [RoomType] ([Id], [TheaterId], [Name], [Description], [CreationTime])
SELECT NEWID(), t.Id, rt.Name, rt.Description, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'2D',   N'Standard 2D projection'),
    (N'3D',   N'3D projection with glasses'),
    (N'IMAX', N'IMAX large-format screen, Dolby Atmos'),
    (N'4DX',  N'Motion seats + environmental effects')
) AS rt(Name, Description);

-- ── Seat types (3 per theater) ────────────────────────────────────────────────
INSERT INTO [SeatType] ([Id], [TheaterId], [Name], [Description], [Color], [PriceMultiplier], [CreationTime])
SELECT NEWID(), t.Id, s.Name, s.Description, s.Color, s.PriceMultiplier, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Standard', N'Regular cinema seat',        N'#3B82F6', 1.0),
    (N'VIP',      N'Extra wide, reclining seat', N'#F59E0B', 1.5),
    (N'Couple',   N'Double-width loveseat (booked as a linked pair)', N'#EC4899', 2.0)
) AS s(Name, Description, Color, PriceMultiplier);

-- ── Food & drinks (per theater) ───────────────────────────────────────────────
INSERT INTO [FoodAndDrink] ([Id], [TheaterId], [Name], [Price], [Description], [IsAvailable], [CreationTime])
SELECT NEWID(), t.Id, f.Name, f.Price, f.Description, f.IsAvailable, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Popcorn (Regular)',     35000, N'Salted or sweet popcorn 500ml',          1),
    (N'Popcorn (Large)',       55000, N'Salted or sweet popcorn 1000ml',         1),
    (N'Coca-Cola',             30000, N'Cold Coca-Cola 500ml',                   1),
    (N'Pepsi',                 30000, N'Cold Pepsi 500ml',                       1),
    (N'Combo (Popcorn+Drink)', 75000, N'Regular popcorn + any 500ml drink',      1),
    (N'Nachos',                45000, N'Nachos with cheese dip 200g',            1),
    (N'Hot Dog',               40000, N'Classic hot dog with mustard & ketchup', 1),
    (N'Caramel Popcorn',       45000, N'Sweet caramel popcorn 500ml',            1)
) AS f(Name, Price, Description, IsAvailable);

-- ── Time slots (per theater) ──────────────────────────────────────────────────
INSERT INTO [TimeSlot] ([Id], [TheaterId], [Name], [StartTime], [EndTime], [CreationTime])
SELECT NEWID(), t.Id, s.Name, s.StartTime, s.EndTime, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Sáng',  N'08:00', N'12:00'),
    (N'Chiều', N'12:00', N'17:00'),
    (N'Tối',   N'17:00', N'23:00')
) AS s(Name, StartTime, EndTime);

-- ── Ticket prices (room type × seat type × time slot × holiday, per theater) ──
-- Explicit price = base 70,000đ × seat multiplier × time-slot factor × room-type
-- factor × holiday factor, rounded to the nearest 1,000đ.
INSERT INTO [TicketPrice] ([Id], [TheaterId], [RoomTypeId], [SeatTypeId], [TimeSlotId], [IsHoliday], [Price], [CreationTime])
SELECT NEWID(), st.TheaterId, rt.Id, st.Id, ts.Id, h.IsHoliday,
       CAST(ROUND(70000 * st.PriceMultiplier
            * CASE ts.Name WHEN N'Tối' THEN 1.2 WHEN N'Sáng' THEN 0.9 ELSE 1.0 END
            * CASE rt.Name WHEN N'IMAX' THEN 1.5 WHEN N'4DX' THEN 1.8 WHEN N'3D' THEN 1.2 ELSE 1.0 END
            * CASE WHEN h.IsHoliday = 1 THEN 1.2 ELSE 1.0 END, -3) AS float),
       GETUTCDATE()
FROM [SeatType] st
JOIN [TimeSlot] ts ON ts.TheaterId = st.TheaterId
JOIN [RoomType] rt ON rt.TheaterId = st.TheaterId
CROSS JOIN (VALUES (CAST(0 AS bit)), (CAST(1 AS bit))) AS h(IsHoliday);

-- ── Rooms ─────────────────────────────────────────────────────────────────────
-- Theater 1: 4 rooms (incl. IMAX)
DECLARE @T1R1 uniqueidentifier = NEWID(); -- 8×12 standard
DECLARE @T1R2 uniqueidentifier = NEWID(); -- 8×12 standard
DECLARE @T1R3 uniqueidentifier = NEWID(); -- 6×10 small
DECLARE @T1R4 uniqueidentifier = NEWID(); -- 10×14 IMAX

-- Theater 2: 3 rooms
DECLARE @T2R1 uniqueidentifier = NEWID(); -- 8×12
DECLARE @T2R2 uniqueidentifier = NEWID(); -- 8×12
DECLARE @T2R3 uniqueidentifier = NEWID(); -- 6×10

-- Theater 3: 3 rooms
DECLARE @T3R1 uniqueidentifier = NEWID(); -- 8×12
DECLARE @T3R2 uniqueidentifier = NEWID(); -- 8×12
DECLARE @T3R3 uniqueidentifier = NEWID(); -- 6×10

INSERT INTO [Room] ([Id], [Name], [TheaterId], [RoomTypeId], [TotalRows], [TotalColumns], [Status], [CreationTime])
SELECT r.Id, r.Name, r.TheaterId,
       (SELECT Id FROM [RoomType] rt WHERE rt.TheaterId = r.TheaterId AND rt.Name = r.TypeName),
       r.Rows, r.Cols, 0, GETUTCDATE()
FROM (VALUES
    (@T1R1, N'Room 1',    @Theater1, N'2D',   8,  12),
    (@T1R2, N'Room 2',    @Theater1, N'3D',   8,  12),
    (@T1R3, N'Room 3',    @Theater1, N'2D',   6,  10),
    (@T1R4, N'IMAX Hall', @Theater1, N'IMAX', 10, 14),
    (@T2R1, N'Room 1',    @Theater2, N'2D',   8,  12),
    (@T2R2, N'Room 2',    @Theater2, N'3D',   8,  12),
    (@T2R3, N'Room 3',    @Theater2, N'2D',   6,  10),
    (@T3R1, N'Room 1',    @Theater3, N'2D',   8,  12),
    (@T3R2, N'Room 2',    @Theater3, N'4DX',  8,  12),
    (@T3R3, N'Room 3',    @Theater3, N'2D',   6,  10)
) AS r(Id, Name, TheaterId, TypeName, Rows, Cols);

-- ── Seats (cross-join approach — no cursor) ───────────────────────────────────

-- Point the scratch seat-type vars at Theater 1's own seat types.
SET @STStandard = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater1 AND Name = N'Standard');
SET @STVIP      = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater1 AND Name = N'VIP');
SET @STCouple   = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater1 AND Name = N'Couple');

-- Theater 1 – Room 1  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T1R1, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 1 – Room 2  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T1R2, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 1 – Room 3  (6 rows × 10 cols, row A = VIP, last col pairs = Couple)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T1R3, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum = 1 THEN @STVIP
            WHEN c.ColNum IN (9,10) THEN @STCouple
            ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 1 – IMAX Hall  (10 rows × 14 cols, rows A-C = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T1R4, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 3 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8),('I',9),('J',10)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) AS c(ColNum);

-- Point the scratch seat-type vars at Theater 2's own seat types.
SET @STStandard = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater2 AND Name = N'Standard');
SET @STVIP      = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater2 AND Name = N'VIP');
SET @STCouple   = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater2 AND Name = N'Couple');

-- Theater 2 – Room 1  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T2R1, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 2 – Room 2  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T2R2, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 2 – Room 3  (6 rows × 10 cols)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T2R3, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Point the scratch seat-type vars at Theater 3's own seat types.
SET @STStandard = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater3 AND Name = N'Standard');
SET @STVIP      = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater3 AND Name = N'VIP');
SET @STCouple   = (SELECT Id FROM [SeatType] WHERE TheaterId = @Theater3 AND Name = N'Couple');

-- Theater 3 – Room 1  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T3R1, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 3 – Room 2  (8 rows × 12 cols, rows A-B = VIP)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T3R2, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 3 – Room 3  (6 rows × 10 cols)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatTypeId],[IsActive],[CreationTime])
SELECT NEWID(), @T3R3, r.RowLetter, c.ColNum,
       CASE WHEN r.RowNum <= 2 THEN @STVIP ELSE @STStandard END, 1, GETUTCDATE()
FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6)) AS r(RowLetter, RowNum)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- ── Movies ────────────────────────────────────────────────────────────────────
-- Helper: base of today (midnight UTC)
DECLARE @Today datetime = CAST(CAST(GETUTCDATE() AS date) AS datetime);

DECLARE @MInception    uniqueidentifier = NEWID();
DECLARE @MDarkKnight   uniqueidentifier = NEWID();
DECLARE @MInterstellar uniqueidentifier = NEWID();
DECLARE @MEndgame      uniqueidentifier = NEWID();
DECLARE @MParasite     uniqueidentifier = NEWID();
DECLARE @MLionKing     uniqueidentifier = NEWID();
DECLARE @MTopGun       uniqueidentifier = NEWID();
DECLARE @MOppenheimer  uniqueidentifier = NEWID();
-- Coming soon
DECLARE @MDunePart2    uniqueidentifier = NEWID();
DECLARE @MInsideOut2   uniqueidentifier = NEWID();
DECLARE @MAQuietPlace  uniqueidentifier = NEWID();

INSERT INTO [Movie] ([Id],[Title],[Description],[Duration],[ReleaseDate],[EndDate],[PosterUrl],[TrailerUrl],
    [Director],[Cast],[Language],[Subtitle],[IsActive],[AgeRestrictionId],[CreationTime]) VALUES

(@MInception,
 N'Inception',
 N'A thief who steals corporate secrets through dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O.',
 148, '2010-07-16', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg',
 N'https://www.youtube.com/watch?v=YoHD9XEInc0',
 N'Christopher Nolan', N'Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page, Tom Hardy',
 N'English', N'Vietnamese', 1, @AgePG, GETUTCDATE()),

(@MDarkKnight,
 N'The Dark Knight',
 N'When the menace known as the Joker wreaks havoc and chaos on Gotham City, Batman must accept one of the greatest psychological and physical tests of his ability to fight injustice.',
 152, '2008-07-18', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/qJ2tW6WMUDux911r6m7haRef0WH.jpg',
 N'https://www.youtube.com/watch?v=EXeTwQWrcwY',
 N'Christopher Nolan', N'Christian Bale, Heath Ledger, Aaron Eckhart, Michael Caine',
 N'English', N'Vietnamese', 1, @AgePG, GETUTCDATE()),

(@MInterstellar,
 N'Interstellar',
 N'A team of explorers travel through a wormhole in space in an attempt to ensure humanity''s survival on a dying Earth.',
 169, '2014-11-07', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg',
 N'https://www.youtube.com/watch?v=zSWdZVtXT7E',
 N'Christopher Nolan', N'Matthew McConaughey, Anne Hathaway, Jessica Chastain, Michael Caine',
 N'English', N'Vietnamese', 1, @AgePG, GETUTCDATE()),

(@MEndgame,
 N'Avengers: Endgame',
 N'After the devastating events of Infinity War, the Avengers assemble once more in order to reverse Thanos'' actions and restore balance to the universe.',
 181, '2019-04-26', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/or06FN3Dka5tukK1e9sl16pB3iy.jpg',
 N'https://www.youtube.com/watch?v=TcMBFSGVi1c',
 N'Anthony Russo, Joe Russo', N'Robert Downey Jr., Chris Evans, Mark Ruffalo, Chris Hemsworth, Scarlett Johansson',
 N'English', N'Vietnamese', 1, @AgePG, GETUTCDATE()),

(@MParasite,
 N'Parasite',
 N'Greed and class discrimination threaten the newly formed symbiotic relationship between the wealthy Park family and the destitute Kim clan.',
 132, '2019-05-30', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg',
 N'https://www.youtube.com/watch?v=5xH0HfJHsaY',
 N'Bong Joon-ho', N'Song Kang-ho, Lee Sun-kyun, Cho Yeo-jeong, Choi Woo-shik',
 N'Korean', N'Vietnamese', 1, @AgeT16, GETUTCDATE()),

(@MLionKing,
 N'The Lion King',
 N'After the murder of his father, a young lion prince flees his kingdom only to learn the true meaning of responsibility and bravery.',
 118, '2019-07-19', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/2bXbqYdUdNVa8VIWXVfclP2ICtT.jpg',
 N'https://www.youtube.com/watch?v=GibiNy4d4gc',
 N'Jon Favreau', N'Donald Glover, Beyoncé, James Earl Jones, Chiwetel Ejiofor',
 N'English', N'Vietnamese', 1, @AgeG, GETUTCDATE()),

(@MTopGun,
 N'Top Gun: Maverick',
 N'After thirty years, Maverick is still pushing the envelope as a top naval aviator, but must confront ghosts of his past when he leads TOP GUN''s elite graduates on a mission that demands the ultimate sacrifice from those chosen to fly it.',
 130, '2022-05-27', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/62HCnUTziyWcpDaBO2i1DX17ljH.jpg',
 N'https://www.youtube.com/watch?v=qSqVVswa420',
 N'Joseph Kosinski', N'Tom Cruise, Miles Teller, Jennifer Connelly, Jon Hamm, Glen Powell',
 N'English', N'Vietnamese', 1, @AgePG, GETUTCDATE()),

(@MOppenheimer,
 N'Oppenheimer',
 N'The story of American scientist J. Robert Oppenheimer and his role in the development of the atomic bomb during World War II.',
 180, '2023-07-21', '2026-12-31',
 N'https://image.tmdb.org/t/p/w500/8Gxv8giaFIzmZTfdcceImngzUz9.jpg',
 N'https://www.youtube.com/watch?v=uYPbbksJxIg',
 N'Christopher Nolan', N'Cillian Murphy, Emily Blunt, Matt Damon, Robert Downey Jr., Florence Pugh',
 N'English', N'Vietnamese', 1, @AgeT13, GETUTCDATE()),

-- Coming Soon
(@MDunePart2,
 N'Dune: Part Two',
 N'Paul Atreides unites with Chani and the Fremen while on a warpath of revenge against the conspirators who destroyed his family.',
 166, DATEADD(month,2,GETUTCDATE()), NULL,
 N'https://image.tmdb.org/t/p/w500/1pdfLvkbY9ohJlCjQH2CZjjYVvJ.jpg',
 N'https://www.youtube.com/watch?v=Way9Dexny3w',
 N'Denis Villeneuve', N'Timothée Chalamet, Zendaya, Rebecca Ferguson, Austin Butler',
 N'English', N'Vietnamese', 1, @AgeT13, GETUTCDATE()),

(@MInsideOut2,
 N'Inside Out 2',
 N'Joy and the other emotions face a new challenge when a new emotion—Anxiety—suddenly shows up unexpectedly.',
 100, DATEADD(month,1,GETUTCDATE()), NULL,
 N'https://image.tmdb.org/t/p/w500/vpnVM9B6NMmQpWeZvzLvDESb2QY.jpg',
 N'https://www.youtube.com/watch?v=LEjhY15eCx0',
 N'Kelsey Mann', N'Amy Poehler, Maya Hawke, Kensington Tallman, Liza Lapira',
 N'English', N'Vietnamese', 1, @AgeG, GETUTCDATE()),

(@MAQuietPlace,
 N'A Quiet Place: Day One',
 N'Experience the day the world went quiet — the origin story of the alien invasion that forced humanity into terrifying silence.',
 99, DATEADD(month,3,GETUTCDATE()), NULL,
 N'https://image.tmdb.org/t/p/w500/yrpPYKijwdMHyTGIOd1iK1h0Xno.jpg',
 N'https://www.youtube.com/watch?v=4L9LCMj9-K8',
 N'Michael Sarnoski', N'Lupita Nyong''o, Joseph Quinn, Alex Wolff, Djimon Hounsou',
 N'English', N'Vietnamese', 1, @AgeT16, GETUTCDATE());

-- ── Movie genre tags ──────────────────────────────────────────────────────────
INSERT INTO [MovieTypeDetail] ([MovieId], [MovieTypeId]) VALUES
(@MInception,   @MTAction), (@MInception,   @MTSciFi),
(@MDarkKnight,  @MTAction), (@MDarkKnight,  @MTThriller),
(@MInterstellar,@MTSciFi),  (@MInterstellar,@MTDrama),
(@MEndgame,     @MTAction), (@MEndgame,     @MTSciFi),
(@MParasite,    @MTThriller),(@MParasite,   @MTDrama),
(@MLionKing,    @MTAnimation),(@MLionKing,  @MTDrama),
(@MTopGun,      @MTAction),  (@MTopGun,     @MTDrama),
(@MOppenheimer, @MTDrama),   (@MOppenheimer,@MTThriller),
(@MDunePart2,   @MTSciFi),   (@MDunePart2,  @MTAction),
(@MInsideOut2,  @MTAnimation),(@MInsideOut2,@MTComedy),
(@MAQuietPlace, @MTHorror),  (@MAQuietPlace,@MTThriller);

-- ── ShowTimes (next 7 days, multiple slots per movie) ─────────────────────────
-- Slots: Morning=9h, Afternoon=14h, Evening=19h, Night=21h30

DECLARE @ST_Inc1  uniqueidentifier = NEWID(); -- Inception today  19:00 T1R1
DECLARE @ST_Inc2  uniqueidentifier = NEWID(); -- Inception +1day  14:00 T2R1
DECLARE @ST_Inc3  uniqueidentifier = NEWID(); -- Inception +2days 21:30 T1R2
DECLARE @ST_DK1   uniqueidentifier = NEWID(); -- Dark Knight today  14:00 T1R2
DECLARE @ST_DK2   uniqueidentifier = NEWID(); -- Dark Knight +1day  19:00 T3R1
DECLARE @ST_IS1   uniqueidentifier = NEWID(); -- Interstellar today  09:00 T1R4 IMAX
DECLARE @ST_IS2   uniqueidentifier = NEWID(); -- Interstellar +2days 19:00 T2R2
DECLARE @ST_Avg1  uniqueidentifier = NEWID(); -- Avengers today  21:30 T1R1
DECLARE @ST_Avg2  uniqueidentifier = NEWID(); -- Avengers +1day  14:00 T3R2
DECLARE @ST_Par1  uniqueidentifier = NEWID(); -- Parasite today  19:00 T2R3
DECLARE @ST_Par2  uniqueidentifier = NEWID(); -- Parasite +3days 21:30 T1R3
DECLARE @ST_LK1   uniqueidentifier = NEWID(); -- Lion King today  09:00 T1R3
DECLARE @ST_LK2   uniqueidentifier = NEWID(); -- Lion King +1day  14:00 T2R3
DECLARE @ST_TG1   uniqueidentifier = NEWID(); -- Top Gun today  14:00 T1R4 IMAX
DECLARE @ST_TG2   uniqueidentifier = NEWID(); -- Top Gun +2days  19:00 T3R1
DECLARE @ST_Opp1  uniqueidentifier = NEWID(); -- Oppenheimer today  19:00 T3R2
DECLARE @ST_Opp2  uniqueidentifier = NEWID(); -- Oppenheimer +1day  21:30 T1R2

INSERT INTO [ShowTime] ([Id],[MovieId],[StartTime],[EndTime],[ProjectionForm],[ShowTimeType],[IsActive],[CreationTime]) VALUES
-- Inception (148 min = 2h28)
(@ST_Inc1, @MInception,    DATEADD(hour,19,@Today),                DATEADD(minute,208,DATEADD(hour,19,@Today)),                1,0,1,GETUTCDATE()),
(@ST_Inc2, @MInception,    DATEADD(hour,14,DATEADD(day,1,@Today)), DATEADD(minute,208,DATEADD(hour,14,DATEADD(day,1,@Today))), 1,0,1,GETUTCDATE()),
(@ST_Inc3, @MInception,    DATEADD(minute,1290,DATEADD(day,2,@Today)), DATEADD(minute,208,DATEADD(minute,1290,DATEADD(day,2,@Today))), 1,0,1,GETUTCDATE()),
-- The Dark Knight (152 min)
(@ST_DK1,  @MDarkKnight,   DATEADD(hour,14,@Today),                DATEADD(minute,212,DATEADD(hour,14,@Today)),                1,0,1,GETUTCDATE()),
(@ST_DK2,  @MDarkKnight,   DATEADD(hour,19,DATEADD(day,1,@Today)), DATEADD(minute,212,DATEADD(hour,19,DATEADD(day,1,@Today))), 1,0,1,GETUTCDATE()),
-- Interstellar (169 min) – IMAX
(@ST_IS1,  @MInterstellar, DATEADD(hour,9,@Today),                 DATEADD(minute,229,DATEADD(hour,9,@Today)),                 1,1,1,GETUTCDATE()),
(@ST_IS2,  @MInterstellar, DATEADD(hour,19,DATEADD(day,2,@Today)), DATEADD(minute,229,DATEADD(hour,19,DATEADD(day,2,@Today))), 1,0,1,GETUTCDATE()),
-- Avengers Endgame (181 min)
(@ST_Avg1, @MEndgame,      DATEADD(minute,1290,@Today),            DATEADD(minute,181,DATEADD(minute,1290,@Today)),            1,0,1,GETUTCDATE()),
(@ST_Avg2, @MEndgame,      DATEADD(hour,14,DATEADD(day,1,@Today)), DATEADD(minute,181,DATEADD(hour,14,DATEADD(day,1,@Today))), 1,0,1,GETUTCDATE()),
-- Parasite (132 min)
(@ST_Par1, @MParasite,     DATEADD(hour,19,@Today),                DATEADD(minute,192,DATEADD(hour,19,@Today)),                1,0,1,GETUTCDATE()),
(@ST_Par2, @MParasite,     DATEADD(minute,1290,DATEADD(day,3,@Today)), DATEADD(minute,192,DATEADD(minute,1290,DATEADD(day,3,@Today))), 1,0,1,GETUTCDATE()),
-- The Lion King (118 min)
(@ST_LK1,  @MLionKing,     DATEADD(hour,9,@Today),                 DATEADD(minute,178,DATEADD(hour,9,@Today)),                 1,0,1,GETUTCDATE()),
(@ST_LK2,  @MLionKing,     DATEADD(hour,14,DATEADD(day,1,@Today)), DATEADD(minute,178,DATEADD(hour,14,DATEADD(day,1,@Today))), 1,0,1,GETUTCDATE()),
-- Top Gun: Maverick (130 min) – IMAX
(@ST_TG1,  @MTopGun,       DATEADD(hour,14,@Today),                DATEADD(minute,190,DATEADD(hour,14,@Today)),                1,0,1,GETUTCDATE()),
(@ST_TG2,  @MTopGun,       DATEADD(hour,19,DATEADD(day,2,@Today)), DATEADD(minute,190,DATEADD(hour,19,DATEADD(day,2,@Today))), 2,0,1,GETUTCDATE()),
-- Oppenheimer (180 min)
(@ST_Opp1, @MOppenheimer,  DATEADD(hour,19,@Today),                DATEADD(minute,240,DATEADD(hour,19,@Today)),                1,1,1,GETUTCDATE()),
(@ST_Opp2, @MOppenheimer,  DATEADD(minute,1290,DATEADD(day,1,@Today)), DATEADD(minute,240,DATEADD(minute,1290,DATEADD(day,1,@Today))), 1,0,1,GETUTCDATE());

-- ── ShowTime → Room mapping with base prices ──────────────────────────────────
INSERT INTO [ShowTimeRoom] ([ShowTimeId], [RoomId], [BasePrice]) VALUES
(@ST_Inc1,  @T1R1, 90000),
(@ST_Inc2,  @T2R1, 90000),
(@ST_Inc3,  @T1R2, 90000),
(@ST_DK1,   @T1R2, 90000),
(@ST_DK2,   @T3R1, 85000),
(@ST_IS1,   @T1R4, 130000),  -- IMAX premium
(@ST_IS2,   @T2R2, 90000),
(@ST_Avg1,  @T1R1, 95000),
(@ST_Avg2,  @T3R2, 85000),
(@ST_Par1,  @T2R3, 80000),
(@ST_Par2,  @T1R3, 80000),
(@ST_LK1,   @T1R3, 75000),
(@ST_LK2,   @T2R3, 75000),
(@ST_TG1,   @T1R4, 130000),  -- IMAX premium
(@ST_TG2,   @T3R1, 100000),  -- 3D
(@ST_Opp1,  @T3R2, 90000),
(@ST_Opp2,  @T1R2, 90000);
