-- ============================================================
-- insert_db.sql  --  Seed data (Guid primary keys, Vietnamese content)
-- Run after create_db.sql to populate with initial/demo data
--
-- Assembled in 4 GO-separated batches (each batch's local DECLAREs are
-- scoped to that batch only; cross-batch lookups go by Name/Code via
-- subqueries against already-inserted rows, never by shared variables).
-- ============================================================

-- ── Age Restrictions ──────────────────────────────────────────────────────────
DECLARE @AgeP   uniqueidentifier = NEWID();
DECLARE @AgeK   uniqueidentifier = NEWID();
DECLARE @AgeT13 uniqueidentifier = NEWID();
DECLARE @AgeT16 uniqueidentifier = NEWID();
DECLARE @AgeT18 uniqueidentifier = NEWID();

INSERT INTO [AgeRestriction] ([Id], [Code], [Description], [MinAge], [CreationTime]) VALUES
(@AgeP,   N'P',   N'Phim được phép phổ biến đến người xem ở mọi độ tuổi', 0,  GETUTCDATE()),
(@AgeK,   N'K',   N'Phim được phổ biến đến người xem dưới 13 tuổi với điều kiện xem cùng cha, mẹ hoặc người giám hộ', 0,  GETUTCDATE()),
(@AgeT13, N'T13', N'Phim cấm phổ biến đến người xem dưới 13 tuổi', 13, GETUTCDATE()),
(@AgeT16, N'T16', N'Phim cấm phổ biến đến người xem dưới 16 tuổi', 16, GETUTCDATE()),
(@AgeT18, N'T18', N'Phim cấm phổ biến đến người xem dưới 18 tuổi', 18, GETUTCDATE());

-- ── User Types ────────────────────────────────────────────────────────────────
DECLARE @UserTypeAdmin    uniqueidentifier = NEWID();
DECLARE @UserTypeCustomer uniqueidentifier = NEWID();
DECLARE @UserTypeStaff    uniqueidentifier = NEWID();

INSERT INTO [UserType] ([Id], [Name], [CreationTime]) VALUES
(@UserTypeAdmin,    N'Admin',        GETUTCDATE()),
(@UserTypeCustomer, N'Customer',     GETUTCDATE()),
(@UserTypeStaff,    N'TheaterStaff', GETUTCDATE()),
(NEWID(),           N'TheaterManager', GETUTCDATE()),
(NEWID(),           N'BoxOfficeStaff', GETUTCDATE()),
(NEWID(),           N'GateStaff', GETUTCDATE()),
(NEWID(),           N'KitchenStaff', GETUTCDATE()),
(NEWID(),           N'RegionalManager', GETUTCDATE());

-- ── Membership tiers ─────────────────────────────────────────────────────────
DECLARE @MemberBronze  uniqueidentifier = NEWID();
DECLARE @MemberSilver  uniqueidentifier = NEWID();
DECLARE @MemberGold    uniqueidentifier = NEWID();
DECLARE @MemberDiamond uniqueidentifier = NEWID();

INSERT INTO [MemberShip] ([Id], [Name], [MinPoints], [MaxPoints], [DiscountPercent], [CreationTime]) VALUES
(@MemberBronze,  N'Hạng Đồng',       0,     999,   0,  GETUTCDATE()),
(@MemberSilver,  N'Hạng Bạc',        1000,  4999,  5,  GETUTCDATE()),
(@MemberGold,    N'Hạng Vàng',       5000,  9999,  10, GETUTCDATE()),
(@MemberDiamond, N'Hạng Kim Cương',  10000, 99999, 15, GETUTCDATE());

-- ── Movie Types ───────────────────────────────────────────────────────────────
DECLARE @MTActionMovie uniqueidentifier = NEWID();
DECLARE @MTComedy      uniqueidentifier = NEWID();
DECLARE @MTDrama       uniqueidentifier = NEWID();
DECLARE @MTHorror      uniqueidentifier = NEWID();
DECLARE @MTSciFi       uniqueidentifier = NEWID();
DECLARE @MTAnimation   uniqueidentifier = NEWID();
DECLARE @MTRomance     uniqueidentifier = NEWID();
DECLARE @MTThriller    uniqueidentifier = NEWID();
DECLARE @MTAdventure   uniqueidentifier = NEWID();
DECLARE @MTMusical     uniqueidentifier = NEWID();

INSERT INTO [MovieType] ([Id], [Name], [CreationTime]) VALUES
(@MTActionMovie, N'Hành Động',              GETUTCDATE()),
(@MTComedy,      N'Hài',                    GETUTCDATE()),
(@MTDrama,       N'Chính Kịch',             GETUTCDATE()),
(@MTHorror,      N'Kinh Dị',                GETUTCDATE()),
(@MTSciFi,       N'Khoa Học Viễn Tưởng',    GETUTCDATE()),
(@MTAnimation,   N'Hoạt Hình',              GETUTCDATE()),
(@MTRomance,     N'Tình Cảm',               GETUTCDATE()),
(@MTThriller,    N'Giật Gân',               GETUTCDATE()),
(@MTAdventure,   N'Phiêu Lưu',              GETUTCDATE()),
(@MTMusical,     N'Nhạc Kịch',              GETUTCDATE());

-- ── Discount Types ────────────────────────────────────────────────────────────
DECLARE @DiscTypePromo     uniqueidentifier = NEWID();
DECLARE @DiscTypeSeasonal  uniqueidentifier = NEWID();
DECLARE @DiscTypeMember    uniqueidentifier = NEWID();

INSERT INTO [DiscountType] ([Id], [Name], [CreationTime]) VALUES
(@DiscTypePromo,    N'Khuyến Mãi',   GETUTCDATE()),
(@DiscTypeSeasonal, N'Theo Mùa',     GETUTCDATE()),
(@DiscTypeMember,   N'Thành Viên',   GETUTCDATE());

-- ── Discounts ─────────────────────────────────────────────────────────────────
INSERT INTO [Discount]
    ([Id], [Code], [Description], [Percent], [MaxDiscountAmount], [DiscountTypeId],
     [StartDate], [EndDate], [MaxUsage], [UsedCount], [IsActive],
     [AutoApply], [ApplyToAllTheaters], [MovieId], [DaysOfWeekMask], [StartTimeOfDay], [EndTimeOfDay],
     [CreationTime])
VALUES
(NEWID(), N'WELCOME10',
    N'Giảm 10% cho thành viên mới', 10, 20000,
    (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'),
    GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), 500, 0, 1,
    0, 1, NULL, NULL, NULL, NULL,
    GETUTCDATE()),
(NEWID(), N'SUMMER20',
    N'Ưu đãi hè - giảm 20%', 20, 40000,
    (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'),
    GETUTCDATE(), DATEADD(month, 3, GETUTCDATE()), 200, 0, 1,
    0, 1, NULL, NULL, NULL, NULL,
    GETUTCDATE()),
(NEWID(), N'STUDENT15',
    N'Giảm 15% khi xuất trình thẻ học sinh, sinh viên', 15, 30000,
    (SELECT Id FROM [DiscountType] WHERE Name = N'Thành Viên'),
    GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), 1000, 0, 1,
    0, 1, NULL, NULL, NULL, NULL,
    GETUTCDATE()),
(NEWID(), N'COUPLE30',
    N'Giảm 30% cho ghế đôi vào cuối tuần', 30, 60000,
    (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'),
    GETUTCDATE(), DATEADD(month, 6, GETUTCDATE()), 100, 0, 1,
    0, 1, NULL, NULL, NULL, NULL,
    GETUTCDATE()),
(NEWID(), N'HAPPYTUESDAY',
    N'Ưu đãi Ngày Vui Thứ Ba - giảm 25% tất cả vé', 25, 50000,
    (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'),
    GETUTCDATE(), DATEADD(month, 6, GETUTCDATE()), NULL, 0, 1,
    1, 1, NULL, 4, NULL, NULL,
    GETUTCDATE());

-- ── Holidays ──────────────────────────────────────────────────────────────────
INSERT INTO [Holiday] ([Id], [Name], [Date], [PriceMultiplier], [CreationTime]) VALUES
(NEWID(), N'Lễ Giáng Sinh',              '2026-12-25', 1.2, GETUTCDATE()),
(NEWID(), N'Tết Dương Lịch',             '2027-01-01', 1.2, GETUTCDATE()),
(NEWID(), N'Tết Nguyên Đán',             '2027-02-06', 1.5, GETUTCDATE()),
(NEWID(), N'Giỗ Tổ Hùng Vương',          '2027-04-15', 1.3, GETUTCDATE()),
(NEWID(), N'Ngày Giải Phóng Miền Nam',   '2027-04-30', 1.3, GETUTCDATE()),
(NEWID(), N'Ngày Quốc Tế Lao Động',      '2027-05-01', 1.3, GETUTCDATE()),
(NEWID(), N'Ngày Quốc Khánh',            '2027-09-02', 1.3, GETUTCDATE());

GO

-- ── Theaters ──────────────────────────────────────────────────────────────────
DECLARE @Theater1 uniqueidentifier = NEWID();
DECLARE @Theater2 uniqueidentifier = NEWID();
DECLARE @Theater3 uniqueidentifier = NEWID();
DECLARE @Theater4 uniqueidentifier = NEWID();
DECLARE @Theater5 uniqueidentifier = NEWID();

INSERT INTO [Theater] ([Id], [Name], [Address], [City], [Phone], [Email], [IsActive], [Latitude], [Longitude], [CreationTime]) VALUES
(@Theater1, N'Cinema Đồng Khởi',          N'19 Đồng Khởi, Phường Bến Nghé, Quận 1',                  N'TP. Hồ Chí Minh', N'028-3822-1900', N'dongkhoi@cinema.vn',  1, 10.7769, 106.7030, GETUTCDATE()),
(@Theater2, N'Cinema Landmark 81',        N'720A Điện Biên Phủ, Phường 22, Bình Thạnh',              N'TP. Hồ Chí Minh', N'028-3512-5678', N'landmark81@cinema.vn', 1, 10.7951, 106.7218, GETUTCDATE()),
(@Theater3, N'Cinema Royal City',         N'72A Nguyễn Trãi, Phường Thượng Đình, Thanh Xuân',        N'Hà Nội',          N'024-3795-9101', N'royalcity@cinema.vn',  1, 21.0016, 105.8126, GETUTCDATE()),
(@Theater4, N'Cinema Vincom Đà Nẵng',     N'910A Ngô Quyền, Phường An Hải Bắc, Sơn Trà',             N'Đà Nẵng',         N'0236-3999-888', N'danang@cinema.vn',     1, 16.0678, 108.2338, GETUTCDATE()),
(@Theater5, N'Cinema Sense City Cần Thơ', N'1 Đại Lộ Hòa Bình, Phường Tân An, Ninh Kiều',            N'Cần Thơ',         N'0292-3812-345', N'cantho@cinema.vn',     1, 10.0341, 105.7881, GETUTCDATE());

-- ── Room types (per theater) ──────────────────────────────────────────────────
INSERT INTO [RoomType] ([Id], [TheaterId], [Name], [Description], [SupportsThreeD], [ThreeDSurcharge], [TurnoverBufferMinutes], [CreationTime])
SELECT NEWID(), t.Id, rt.Name, rt.Description, rt.SupportsThreeD, rt.ThreeDSurcharge, rt.TurnoverBufferMinutes, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'2D',     N'Phòng chiếu tiêu chuẩn 2D',                                                      0, 0,     10),
    (N'3D',     N'Phòng chiếu 3D có kính chuyên dụng',                                              1, 30000, 15),
    (N'IMAX',   N'Màn hình IMAX khổ lớn, âm thanh Dolby Atmos',                                     1, 40000, 20),
    (N'4DX',    N'Ghế chuyển động kết hợp hiệu ứng môi trường (gió, nước, mùi hương)',               1, 40000, 25),
    (N'Deluxe', N'Phòng chiếu cao cấp, ghế bọc da thư giãn sang trọng',                              0, 0,     15)
) AS rt(Name, Description, SupportsThreeD, ThreeDSurcharge, TurnoverBufferMinutes);

-- ── Seat types (exactly 2 per theater) ────────────────────────────────────────
INSERT INTO [SeatType] ([Id], [TheaterId], [Kind], [Name], [Description], [Color], [CreationTime])
SELECT NEWID(), t.Id, s.Kind, s.Name, s.Description, s.Color, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (0, N'Ghế Đơn', N'Ghế ngồi tiêu chuẩn dành cho một người',        N'#3B82F6'),
    (1, N'Ghế Đôi', N'Ghế đôi liền kề, đặt theo cặp liên kết',        N'#EC4899')
) AS s(Kind, Name, Description, Color);

-- ── Patron categories (per theater, one row per (name, seat kind) combination) ──
INSERT INTO [PatronCategory] ([Id], [TheaterId], [SeatTypeId], [Name], [Price], [IsActive], [CreationTime])
SELECT NEWID(), t.Id, st.Id, c.Name, c.Price, 1, GETUTCDATE()
FROM [Theater] t
JOIN [SeatType] st ON st.TheaterId = t.Id
CROSS JOIN (VALUES
    (N'Người Lớn',              0, 90000),
    (N'Người Lớn',              1, 170000),
    (N'Học Sinh - Sinh Viên',   0, 65000),
    (N'Người Cao Tuổi',         0, 60000),
    (N'Người Cao Tuổi',         1, 115000),
    (N'Trẻ Em',                 0, 50000)
) AS c(Name, Kind, Price)
WHERE st.Kind = c.Kind;

-- ── Food & drinks (per theater) ───────────────────────────────────────────────
INSERT INTO [FoodAndDrink] ([Id], [TheaterId], [Name], [Price], [Description], [IsAvailable], [CreationTime])
SELECT NEWID(), t.Id, f.Name, f.Price, f.Description, f.IsAvailable, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Bắp Rang Thường',       35000, N'Bắp rang bơ hoặc mặn 500ml',                          1),
    (N'Bắp Rang Lớn',          55000, N'Bắp rang bơ hoặc mặn 1000ml',                          1),
    (N'Coca-Cola',             30000, N'Coca-Cola lạnh 500ml',                                 1),
    (N'Pepsi',                 30000, N'Pepsi lạnh 500ml',                                     1),
    (N'Combo Bắp Nước',        75000, N'Bắp rang thường kèm 1 nước ngọt 500ml',                1),
    (N'Nachos Phô Mai',        45000, N'Bánh nachos kèm sốt phô mai 200g',                     1),
    (N'Hot Dog',               40000, N'Hot dog kiểu Mỹ kèm mù tạt và tương cà',                1),
    (N'Bắp Rang Caramel',      45000, N'Bắp rang phủ caramel ngọt 500ml',                       1),
    (N'Trà Đào Cam Sả',        42000, N'Trà đào cam sả mát lạnh 500ml',                         1),
    (N'Snack Khoai Tây',       32000, N'Khoai tây chiên giòn phần lớn',                          1),
    (N'Kẹo Dẻo Trái Cây',      25000, N'Kẹo dẻo hương trái cây tổng hợp 150g',                  1),
    (N'Combo Gia Đình',       150000, N'2 bắp rang lớn kèm 4 nước ngọt 500ml',                  1)
) AS f(Name, Price, Description, IsAvailable);

-- ── Seeded combos: flag the two combo items and give them a recipe (tracking stays off) ──
UPDATE [FoodAndDrink] SET [IsCombo] = 1 WHERE [Name] IN (N'Combo Bắp Nước', N'Combo Gia Đình');

INSERT INTO [ComboItem] ([Id], [ComboId], [ComponentId], [Quantity], [CreationTime])
SELECT NEWID(), combo.Id, comp.Id, r.Quantity, GETUTCDATE()
FROM (VALUES
    (N'Combo Bắp Nước', N'Bắp Rang Thường', 1),
    (N'Combo Bắp Nước', N'Coca-Cola',       1),
    (N'Combo Gia Đình', N'Bắp Rang Lớn',    2),
    (N'Combo Gia Đình', N'Coca-Cola',       4)
) AS r(ComboName, ComponentName, Quantity)
JOIN [FoodAndDrink] combo ON combo.[Name] = r.ComboName
JOIN [FoodAndDrink] comp  ON comp.[Name] = r.ComponentName AND comp.[TheaterId] = combo.[TheaterId];

-- ── Time slots (per theater) ──────────────────────────────────────────────────
INSERT INTO [TimeSlot] ([Id], [TheaterId], [Name], [StartTime], [EndTime], [CreationTime])
SELECT NEWID(), t.Id, ts.Name, ts.StartTime, ts.EndTime, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Sáng', N'08:00', N'12:00'),
    (N'Chiều', N'12:00', N'18:00'),
    (N'Tối',  N'18:00', N'22:00'),
    (N'Khuya', N'22:00', N'23:59')
) AS ts(Name, StartTime, EndTime);

-- ── Ticket prices (time-of-day/holiday multiplier per theater/room type/time slot) ──
INSERT INTO [TicketPrice] ([Id], [TheaterId], [RoomTypeId], [TimeSlotId], [IsHoliday], [PriceMultiplier], [CreationTime])
SELECT NEWID(), rt.TheaterId, rt.Id, ts.Id, h.IsHoliday,
    (CASE ts.Name WHEN N'Tối' THEN 1.2 WHEN N'Khuya' THEN 1.3 WHEN N'Sáng' THEN 0.9 ELSE 1.0 END)
    * (CASE rt.Name WHEN N'IMAX' THEN 1.5 WHEN N'4DX' THEN 1.8 WHEN N'Deluxe' THEN 1.4 WHEN N'3D' THEN 1.2 ELSE 1.0 END)
    * (CASE WHEN h.IsHoliday = 1 THEN 1.2 ELSE 1.0 END),
    GETUTCDATE()
FROM [RoomType] rt
JOIN [TimeSlot] ts ON ts.TheaterId = rt.TheaterId
CROSS JOIN (VALUES (0), (1)) AS h(IsHoliday);

-- ── Room type × patron category prices (default = category's base price) ─────
INSERT INTO [RoomTypePatronCategoryPrice] ([Id], [RoomTypeId], [PatronCategoryId], [Price], [CreationTime])
SELECT NEWID(), rt.Id, pc.Id, pc.Price, GETUTCDATE()
FROM [RoomType] rt
JOIN [PatronCategory] pc ON pc.TheaterId = rt.TheaterId AND pc.IsActive = 1;

-- ── Bump premium room-type overrides for Người Lớn (demonstrates per-room-type override) ──
UPDATE rtpcp
SET rtpcp.Price = CASE WHEN pc.Price >= 100000 THEN ROUND(pc.Price * 1.3, -3) ELSE ROUND(pc.Price * 1.5, -3) END
FROM [RoomTypePatronCategoryPrice] rtpcp
JOIN [RoomType] rt ON rt.Id = rtpcp.RoomTypeId
JOIN [PatronCategory] pc ON pc.Id = rtpcp.PatronCategoryId
WHERE rt.Name IN (N'IMAX', N'4DX', N'Deluxe') AND pc.Name = N'Người Lớn';

GO

-- ── Part C: Rooms + Seats ──────────────────────────────────────────────────

DECLARE @Theater1 uniqueidentifier = (SELECT Id FROM [Theater] WHERE Name = N'Cinema Đồng Khởi');
DECLARE @Theater2 uniqueidentifier = (SELECT Id FROM [Theater] WHERE Name = N'Cinema Landmark 81');
DECLARE @Theater3 uniqueidentifier = (SELECT Id FROM [Theater] WHERE Name = N'Cinema Royal City');
DECLARE @Theater4 uniqueidentifier = (SELECT Id FROM [Theater] WHERE Name = N'Cinema Vincom Đà Nẵng');
DECLARE @Theater5 uniqueidentifier = (SELECT Id FROM [Theater] WHERE Name = N'Cinema Sense City Cần Thơ');

-- Theater 1: Cinema Đồng Khởi
DECLARE @T1_R1 uniqueidentifier = NEWID();
DECLARE @T1_R2 uniqueidentifier = NEWID();
DECLARE @T1_R3 uniqueidentifier = NEWID();
DECLARE @T1_IMAX uniqueidentifier = NEWID();
DECLARE @T1_Deluxe uniqueidentifier = NEWID();

-- Theater 2: Cinema Landmark 81
DECLARE @T2_R1 uniqueidentifier = NEWID();
DECLARE @T2_R2 uniqueidentifier = NEWID();
DECLARE @T2_R3 uniqueidentifier = NEWID();
DECLARE @T2_4DX uniqueidentifier = NEWID();

-- Theater 3: Cinema Royal City
DECLARE @T3_R1 uniqueidentifier = NEWID();
DECLARE @T3_R2 uniqueidentifier = NEWID();
DECLARE @T3_R3 uniqueidentifier = NEWID();
DECLARE @T3_Deluxe uniqueidentifier = NEWID();

-- Theater 4: Cinema Vincom Đà Nẵng
DECLARE @T4_R1 uniqueidentifier = NEWID();
DECLARE @T4_R2 uniqueidentifier = NEWID();
DECLARE @T4_R3 uniqueidentifier = NEWID();

-- Theater 5: Cinema Sense City Cần Thơ
DECLARE @T5_R1 uniqueidentifier = NEWID();
DECLARE @T5_R2 uniqueidentifier = NEWID();
DECLARE @T5_R3 uniqueidentifier = NEWID();

INSERT INTO [Room] ([Id],[Name],[TheaterId],[RoomTypeId],[TotalRows],[TotalColumns],[Status],[CreationTime])
SELECT r.Id, r.Name, r.TheaterId,
       (SELECT Id FROM [RoomType] rt WHERE rt.TheaterId = r.TheaterId AND rt.Name = r.TypeName),
       r.Rows, r.Cols, 0, GETUTCDATE()
FROM (VALUES
    (@T1_R1,     N'Phòng 1',      @Theater1, N'2D',    8, 12),
    (@T1_R2,     N'Phòng 2',      @Theater1, N'3D',    8, 12),
    (@T1_R3,     N'Phòng 3',      @Theater1, N'2D',    6, 10),
    (@T1_IMAX,   N'Phòng IMAX',   @Theater1, N'IMAX', 10, 14),
    (@T1_Deluxe, N'Phòng Deluxe', @Theater1, N'Deluxe', 6, 8),

    (@T2_R1,   N'Phòng 1',   @Theater2, N'2D',   8, 12),
    (@T2_R2,   N'Phòng 2',   @Theater2, N'3D',   8, 12),
    (@T2_R3,   N'Phòng 3',   @Theater2, N'2D',   6, 10),
    (@T2_4DX,  N'Phòng 4DX', @Theater2, N'4DX',  8, 10),

    (@T3_R1,     N'Phòng 1',      @Theater3, N'2D',    8, 12),
    (@T3_R2,     N'Phòng 2',      @Theater3, N'4DX',   8, 12),
    (@T3_R3,     N'Phòng 3',      @Theater3, N'2D',    6, 10),
    (@T3_Deluxe, N'Phòng Deluxe', @Theater3, N'Deluxe', 6, 8),

    (@T4_R1, N'Phòng 1', @Theater4, N'2D', 8, 12),
    (@T4_R2, N'Phòng 2', @Theater4, N'3D', 8, 12),
    (@T4_R3, N'Phòng 3', @Theater4, N'2D', 6, 10),

    (@T5_R1, N'Phòng 1', @Theater5, N'2D', 8, 12),
    (@T5_R2, N'Phòng 2', @Theater5, N'2D', 6, 10),
    (@T5_R3, N'Phòng 3', @Theater5, N'3D', 8, 10)
) AS r(Id, Name, TheaterId, TypeName, Rows, Cols);

-- ── Seats ───────────────────────────────────────────────────────────────────

-- Theater 1 / Phòng 1 (2D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T1_R1, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 1 / Phòng 2 (3D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T1_R2, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 1 / Phòng 3 (2D, 6x10) — Double-seat demo: cols 9,10 paired per row
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatGroupId],[IsActive],[CreationTime])
SELECT NEWID(), @T1_R3, r.RowLetter, c.ColNum,
       CASE WHEN c.ColNum IN (9,10) THEN r.PairGroupId ELSE NULL END, 1, GETUTCDATE()
FROM (VALUES ('A',NEWID()),('B',NEWID()),('C',NEWID()),('D',NEWID()),('E',NEWID()),('F',NEWID())) AS r(RowLetter, PairGroupId)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 1 / Phòng IMAX (IMAX, 10x14)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T1_IMAX, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H'),('I'),('J')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) AS c(ColNum);

-- Theater 1 / Phòng Deluxe (Deluxe, 6x8)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T1_Deluxe, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) AS c(ColNum);

-- Theater 2 / Phòng 1 (2D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T2_R1, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 2 / Phòng 2 (3D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T2_R2, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 2 / Phòng 3 (2D, 6x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T2_R3, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 2 / Phòng 4DX (4DX, 8x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T2_4DX, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 3 / Phòng 1 (2D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T3_R1, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 3 / Phòng 2 (4DX, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T3_R2, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 3 / Phòng 3 (2D, 6x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T3_R3, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 3 / Phòng Deluxe (Deluxe, 6x8)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T3_Deluxe, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) AS c(ColNum);

-- Theater 4 / Phòng 1 (2D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T4_R1, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 4 / Phòng 2 (3D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T4_R2, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 4 / Phòng 3 (2D, 6x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T4_R3, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 5 / Phòng 1 (2D, 8x12)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T5_R1, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS c(ColNum);

-- Theater 5 / Phòng 2 (2D, 6x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T5_R2, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

-- Theater 5 / Phòng 3 (3D, 8x10)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), @T5_R3, r.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM (VALUES ('A'),('B'),('C'),('D'),('E'),('F'),('G'),('H')) AS r(RowLetter)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS c(ColNum);

GO

-- ── Part D: Movies, Genres, ShowTimes, ShowTimeRooms ──

DECLARE @Today datetime = CAST(CAST(GETUTCDATE() AS date) AS datetime);

-- ── Lookups: AgeRestriction ──
DECLARE @AgeP   uniqueidentifier = (SELECT Id FROM [AgeRestriction] WHERE Code = N'P');
DECLARE @AgeK   uniqueidentifier = (SELECT Id FROM [AgeRestriction] WHERE Code = N'K');
DECLARE @AgeT13 uniqueidentifier = (SELECT Id FROM [AgeRestriction] WHERE Code = N'T13');
DECLARE @AgeT16 uniqueidentifier = (SELECT Id FROM [AgeRestriction] WHERE Code = N'T16');
DECLARE @AgeT18 uniqueidentifier = (SELECT Id FROM [AgeRestriction] WHERE Code = N'T18');

-- ── Lookups: MovieType ──
DECLARE @MT_HanhDong  uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Hành Động');
DECLARE @MT_Hai       uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Hài');
DECLARE @MT_ChinhKich uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Chính Kịch');
DECLARE @MT_KinhDi    uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Kinh Dị');
DECLARE @MT_KhoaHoc   uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Khoa Học Viễn Tưởng');
DECLARE @MT_HoatHinh  uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Hoạt Hình');
DECLARE @MT_TinhCam   uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Tình Cảm');
DECLARE @MT_GiatGan   uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Giật Gân');
DECLARE @MT_PhieuLuu  uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Phiêu Lưu');
DECLARE @MT_NhacKich  uniqueidentifier = (SELECT Id FROM [MovieType] WHERE Name = N'Nhạc Kịch');

-- ── Lookups: Rooms (by Theater.Name + Room.Name) ──
DECLARE @DK_P1   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Đồng Khởi' AND r.Name = N'Phòng 1');
DECLARE @DK_P2   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Đồng Khởi' AND r.Name = N'Phòng 2');
DECLARE @DK_P3   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Đồng Khởi' AND r.Name = N'Phòng 3');
DECLARE @DK_IMAX uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Đồng Khởi' AND r.Name = N'Phòng IMAX');
DECLARE @DK_DLX  uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Đồng Khởi' AND r.Name = N'Phòng Deluxe');

DECLARE @LM_P1   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Landmark 81' AND r.Name = N'Phòng 1');
DECLARE @LM_P2   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Landmark 81' AND r.Name = N'Phòng 2');
DECLARE @LM_P3   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Landmark 81' AND r.Name = N'Phòng 3');
DECLARE @LM_4DX  uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Landmark 81' AND r.Name = N'Phòng 4DX');

DECLARE @RC_P1   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Royal City' AND r.Name = N'Phòng 1');
DECLARE @RC_P2   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Royal City' AND r.Name = N'Phòng 2');
DECLARE @RC_P3   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Royal City' AND r.Name = N'Phòng 3');
DECLARE @RC_DLX  uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Royal City' AND r.Name = N'Phòng Deluxe');

DECLARE @DN_P1   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Vincom Đà Nẵng' AND r.Name = N'Phòng 1');
DECLARE @DN_P2   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Vincom Đà Nẵng' AND r.Name = N'Phòng 2');
DECLARE @DN_P3   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Vincom Đà Nẵng' AND r.Name = N'Phòng 3');

DECLARE @CT_P1   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Sense City Cần Thơ' AND r.Name = N'Phòng 1');
DECLARE @CT_P2   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Sense City Cần Thơ' AND r.Name = N'Phòng 2');
DECLARE @CT_P3   uniqueidentifier = (SELECT r.Id FROM [Room] r JOIN [Theater] t ON t.Id = r.TheaterId WHERE t.Name = N'Cinema Sense City Cần Thơ' AND r.Name = N'Phòng 3');

-- ── Movie Ids + Durations (declared up front, reused for MovieTypeDetail and ShowTime) ──
DECLARE @M1  uniqueidentifier = NEWID(); DECLARE @M1Dur  int = 148; -- Inception
DECLARE @M2  uniqueidentifier = NEWID(); DECLARE @M2Dur  int = 152; -- The Dark Knight
DECLARE @M3  uniqueidentifier = NEWID(); DECLARE @M3Dur  int = 169; -- Interstellar
DECLARE @M4  uniqueidentifier = NEWID(); DECLARE @M4Dur  int = 181; -- Avengers: Endgame
DECLARE @M5  uniqueidentifier = NEWID(); DECLARE @M5Dur  int = 132; -- Parasite
DECLARE @M6  uniqueidentifier = NEWID(); DECLARE @M6Dur  int = 118; -- The Lion King
DECLARE @M7  uniqueidentifier = NEWID(); DECLARE @M7Dur  int = 130; -- Top Gun: Maverick
DECLARE @M8  uniqueidentifier = NEWID(); DECLARE @M8Dur  int = 180; -- Oppenheimer
DECLARE @M9  uniqueidentifier = NEWID(); DECLARE @M9Dur  int = 140; -- Spider-Man: Across the Spider-Verse
DECLARE @M10 uniqueidentifier = NEWID(); DECLARE @M10Dur int = 128; -- Deadpool & Wolverine
DECLARE @M11 uniqueidentifier = NEWID(); DECLARE @M11Dur int = 160; -- Wicked
DECLARE @M12 uniqueidentifier = NEWID(); DECLARE @M12Dur int = 97;  -- Bố Già
DECLARE @M13 uniqueidentifier = NEWID(); DECLARE @M13Dur int = 100; -- Đào, Phở và Piano
DECLARE @M14 uniqueidentifier = NEWID(); DECLARE @M14Dur int = 166; -- Dune: Part Two (sắp chiếu)
DECLARE @M15 uniqueidentifier = NEWID(); DECLARE @M15Dur int = 96;  -- Inside Out 2 (sắp chiếu)
DECLARE @M16 uniqueidentifier = NEWID(); DECLARE @M16Dur int = 100; -- A Quiet Place: Day One (sắp chiếu)
DECLARE @M17 uniqueidentifier = NEWID(); DECLARE @M17Dur int = 131; -- Mai (sắp chiếu)
DECLARE @M18 uniqueidentifier = NEWID(); DECLARE @M18Dur int = 148; -- Gladiator II (sắp chiếu)

-- ── 1. Movie ──
INSERT INTO [Movie] (Id, Title, Description, Duration, ReleaseDate, EndDate, PosterUrl, TrailerUrl, Director, Cast, Language, Subtitle, IsActive, AgeRestrictionId, CreationTime)
VALUES
(@M1, N'Inception', N'Một tên trộm chuyên đánh cắp bí mật thông qua công nghệ chia sẻ giấc mơ được giao nhiệm vụ ngược lại: gieo một ý tưởng vào tâm trí mục tiêu.', @M1Dur, DATEADD(day,-120,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg', N'https://www.youtube.com/watch?v=YoHD9XEInc0', N'Christopher Nolan', N'Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M2, N'The Dark Knight', N'Batman đối đầu với Joker, tên tội phạm điên loạn muốn đẩy Gotham vào hỗn loạn, buộc anh phải đối mặt với những giới hạn đạo đức của chính mình.', @M2Dur, DATEADD(day,-200,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/qJ2tW6WMUDux911r6m7haRef0WH.jpg', N'https://www.youtube.com/watch?v=EXeTwQWrcwY', N'Christopher Nolan', N'Christian Bale, Heath Ledger, Aaron Eckhart', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M3, N'Interstellar', N'Một nhóm phi hành gia du hành qua hố sâu vũ trụ để tìm kiếm ngôi nhà mới cho nhân loại khi Trái Đất đang dần trở nên không thể sinh sống.', @M3Dur, DATEADD(day,-60,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg', N'https://www.youtube.com/watch?v=zSWdZVtXT7E', N'Christopher Nolan', N'Matthew McConaughey, Anne Hathaway, Jessica Chastain', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M4, N'Avengers: Endgame', N'Các siêu anh hùng còn sống sót tập hợp để đảo ngược thiệt hại mà Thanos đã gây ra, trong trận chiến cuối cùng quyết định vận mệnh vũ trụ.', @M4Dur, DATEADD(day,-30,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/or06FN3Dka5tukK1e9sl16pB3iy.jpg', N'https://www.youtube.com/watch?v=TcMBFSGVi1c', N'Anthony Russo, Joe Russo', N'Robert Downey Jr., Chris Evans, Scarlett Johansson', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M5, N'Parasite', N'Câu chuyện về gia đình nghèo khó tìm cách len lỏi vào cuộc sống của một gia đình giàu có, dẫn đến những hệ lụy bất ngờ và đầy ám ảnh.', @M5Dur, DATEADD(day,-90,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg', N'https://www.youtube.com/watch?v=5xH0HfJHsaY', N'Bong Joon-ho', N'Song Kang-ho, Lee Sun-kyun, Cho Yeo-jeong', N'Tiếng Hàn', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M6, N'The Lion King', N'Chú sư tử con Simba phải trưởng thành và đối mặt với quá khứ để giành lại ngôi vị vua của vùng đất tổ tiên.', @M6Dur, DATEADD(day,-15,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/2bXbqYdUdNVL8OYVIzdgi2xg1kY.jpg', N'https://www.youtube.com/watch?v=7TavVZMewpY', N'Jon Favreau', N'Donald Glover, Beyoncé, Chiwetel Ejiofor', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeP, GETUTCDATE()),
(@M7, N'Top Gun: Maverick', N'Phi công huyền thoại Maverick trở lại huấn luyện thế hệ phi công mới cho một nhiệm vụ liều lĩnh bậc nhất sự nghiệp.', @M7Dur, DATEADD(day,-45,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/62HCnUTziyWcpDaBO2i1DX17ljH.jpg', N'https://www.youtube.com/watch?v=giXco2jaZ_4', N'Joseph Kosinski', N'Tom Cruise, Miles Teller, Jennifer Connelly', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M8, N'Oppenheimer', N'Câu chuyện về nhà vật lý J. Robert Oppenheimer và vai trò của ông trong việc phát triển bom nguyên tử, cùng những giằng xé đạo đức sau đó.', @M8Dur, DATEADD(day,-70,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/8Gxv8gSFCU0XGDykEGv7zR1n2ua.jpg', N'https://www.youtube.com/watch?v=uYPbbksJxIg', N'Christopher Nolan', N'Cillian Murphy, Emily Blunt, Matt Damon', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M9, N'Spider-Man: Across the Spider-Verse', N'Miles Morales du hành qua đa vũ trụ cùng Gwen Stacy, đối mặt với một phản diện mới và số phận đầy tranh cãi của chính mình.', @M9Dur, DATEADD(day,-25,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/8Vt6mWEReuy4Of61Lnj5Xj16sVK.jpg', N'https://www.youtube.com/watch?v=cqGjhVJWtEg', N'Joaquim Dos Santos, Kemp Powers', N'Shameik Moore, Hailee Steinfeld', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeK, GETUTCDATE()),
(@M10, N'Deadpool & Wolverine', N'Deadpool bắt tay cùng Wolverine trong một hành trình hài hước, bạo lực và đầy bất ngờ xuyên suốt đa vũ trụ Marvel.', @M10Dur, DATEADD(day,-10,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/8cdWjvZQUExUUTzyp4t6EDMubfO.jpg', N'https://www.youtube.com/watch?v=73_1biulkYk', N'Shawn Levy', N'Ryan Reynolds, Hugh Jackman', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT18, GETUTCDATE()),
(@M11, N'Wicked', N'Câu chuyện về tình bạn giữa Elphaba và Glinda trước khi một người trở thành Phù thủy Xứ Oz, kể lại qua âm nhạc đầy màu sắc.', @M11Dur, DATEADD(day,-5,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/xDGbZ0JJ3mYaGKy4Nzd9Kph6EOo.jpg', N'https://www.youtube.com/watch?v=6COmYeLsyc8', N'Jon M. Chu', N'Cynthia Erivo, Ariana Grande', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeK, GETUTCDATE()),
(@M12, N'Bố Già', N'Bộ phim hài - chính kịch xoay quanh những mâu thuẫn và tình cảm gia đình trong một con hẻm nhỏ ở Sài Gòn.', @M12Dur, DATEADD(day,-80,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/lJloDgW05Rht0GmA22Bo0j9nY0z.jpg', N'https://www.youtube.com/watch?v=EFbdOSHzhtA', N'Trấn Thành, Vũ Ngọc Đãng', N'Trấn Thành, Tuấn Trần, Ngân Chi', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M13, N'Đào, Phở và Piano', N'Bộ phim tái hiện những ngày cuối cùng của trận chiến Hà Nội mùa đông 1946, khắc họa tình yêu và lòng quả cảm giữa khói lửa chiến tranh.', @M13Dur, DATEADD(day,-40,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/5fzZBpwWfCVQt1RustGgqSVfoDT.jpg', N'https://www.youtube.com/watch?v=1fGh6vXwCsE', N'Phi Tiến Sơn', N'Doãn Quốc Đam, Cao Thị Thùy Linh', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M14, N'Dune: Part Two', N'Paul Atreides tiếp tục hành trình trả thù và định mệnh của mình trên hành tinh sa mạc Arrakis, dẫn dắt người Fremen chống lại nhà Harkonnen.', @M14Dur, DATEADD(month,2,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/1pdfLvkbY9ohJlCjQH2CZjjYVvJ.jpg', N'https://www.youtube.com/watch?v=Way9Dexny3w', N'Denis Villeneuve', N'Timothée Chalamet, Zendaya, Rebecca Ferguson', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M15, N'Inside Out 2', N'Riley bước vào tuổi dậy thì và phải học cách đối diện với những cảm xúc mới xuất hiện trong tâm trí, bên cạnh những cảm xúc quen thuộc.', @M15Dur, DATEADD(month,1,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/vpnVM9B6NMmQpWeZvzLvDESb2QY.jpg', N'https://www.youtube.com/watch?v=LEjhY15eCx0', N'Kelsey Mann', N'Amy Poehler, Maya Hawke', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeP, GETUTCDATE()),
(@M16, N'A Quiet Place: Day One', N'Câu chuyện tiền truyện kể về ngày đầu tiên loài quái vật săn mồi bằng âm thanh xâm chiếm Trái Đất, qua góc nhìn của những người sống sót đầu tiên.', @M16Dur, DATEADD(month,3,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/yrpPYKijwdMHyTGxWLpxPS1shHU.jpg', N'https://www.youtube.com/watch?v=Xzt2P1sxaHY', N'Michael Sarnoski', N'Lupita Nyong''o, Joseph Quinn', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M17, N'Mai', N'Bộ phim chính kịch - tình cảm kể về hành trình tìm kiếm hạnh phúc của một người phụ nữ với quá khứ đầy tổn thương.', @M17Dur, DATEADD(month,1,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/9nRvNS2GJ3B7pcNb8m8H9pF1bqK.jpg', N'https://www.youtube.com/watch?v=DVfLJFHVTdI', N'Trấn Thành', N'Phương Anh Đào, Tuấn Trần', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M18, N'Gladiator II', N'Hậu duệ của Maximus bị đẩy vào đấu trường La Mã và buộc phải chiến đấu để giành lại danh dự cùng tương lai của đế chế.', @M18Dur, DATEADD(month,2,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/2cxhvwyEwRlysAmRH4iodkvo0z5.jpg', N'https://www.youtube.com/watch?v=4jrE-Fu2XCA', N'Ridley Scott', N'Paul Mescal, Denzel Washington, Pedro Pascal', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE());

-- ── 2. MovieTypeDetail ──
INSERT INTO [MovieTypeDetail] (MovieId, MovieTypeId) VALUES
(@M1, @MT_HanhDong), (@M1, @MT_KhoaHoc),
(@M2, @MT_HanhDong), (@M2, @MT_GiatGan),
(@M3, @MT_KhoaHoc), (@M3, @MT_ChinhKich),
(@M4, @MT_HanhDong), (@M4, @MT_PhieuLuu),
(@M5, @MT_ChinhKich), (@M5, @MT_GiatGan),
(@M6, @MT_HoatHinh),
(@M7, @MT_HanhDong),
(@M8, @MT_ChinhKich),
(@M9, @MT_HoatHinh), (@M9, @MT_PhieuLuu),
(@M10, @MT_HanhDong), (@M10, @MT_Hai),
(@M11, @MT_NhacKich), (@M11, @MT_TinhCam),
(@M12, @MT_Hai), (@M12, @MT_ChinhKich),
(@M13, @MT_ChinhKich),
(@M14, @MT_KhoaHoc), (@M14, @MT_PhieuLuu),
(@M15, @MT_HoatHinh),
(@M16, @MT_KinhDi), (@M16, @MT_GiatGan),
(@M17, @MT_ChinhKich), (@M17, @MT_TinhCam),
(@M18, @MT_HanhDong);

-- ── 3. ShowTime (only for the 13 "đang chiếu" movies) ──
DECLARE @S1  uniqueidentifier = NEWID(); -- Inception, Đồng Khởi P1, day0 14:00
DECLARE @S2  uniqueidentifier = NEWID(); -- Inception, Đồng Khởi IMAX, day1 21:30, 3D
DECLARE @S3  uniqueidentifier = NEWID(); -- Inception, Landmark81 P1, day2 19:00
DECLARE @S4  uniqueidentifier = NEWID(); -- Dark Knight, Landmark81 P2, day0 19:00
DECLARE @S5  uniqueidentifier = NEWID(); -- Dark Knight, Royal City P1, day3 14:00
DECLARE @S6  uniqueidentifier = NEWID(); -- Dark Knight, Cần Thơ P3, day5 21:30
DECLARE @S7  uniqueidentifier = NEWID(); -- Interstellar, Đồng Khởi P2, day1 09:00
DECLARE @S8  uniqueidentifier = NEWID(); -- Interstellar, Landmark81 4DX, day3 21:30, 3D
DECLARE @S9  uniqueidentifier = NEWID(); -- Avengers Endgame, Đồng Khởi P3, day0 09:00
DECLARE @S10 uniqueidentifier = NEWID(); -- Avengers Endgame, Royal City Deluxe, day2 19:00, 3D
DECLARE @S11 uniqueidentifier = NEWID(); -- Avengers Endgame, Cần Thơ P1, day4 14:00
DECLARE @S12 uniqueidentifier = NEWID(); -- Parasite, Royal City P2, day1 21:30
DECLARE @S13 uniqueidentifier = NEWID(); -- Parasite, Đà Nẵng P1, day4 19:00
DECLARE @S14 uniqueidentifier = NEWID(); -- Parasite, Đồng Khởi P2, day3 09:00
DECLARE @S15 uniqueidentifier = NEWID(); -- Lion King, Đồng Khởi P1, day2 09:00, Premiere
DECLARE @S16 uniqueidentifier = NEWID(); -- Lion King, Cần Thơ P2, day0 14:00
DECLARE @S17 uniqueidentifier = NEWID(); -- Top Gun Maverick, Landmark81 4DX, day1 14:00, 3D
DECLARE @S18 uniqueidentifier = NEWID(); -- Top Gun Maverick, Đồng Khởi Deluxe, day4 21:30, 3D
DECLARE @S19 uniqueidentifier = NEWID(); -- Top Gun Maverick, Đà Nẵng P2, day2 19:00
DECLARE @S20 uniqueidentifier = NEWID(); -- Oppenheimer, Royal City P3, day0 21:30
DECLARE @S21 uniqueidentifier = NEWID(); -- Oppenheimer, Cần Thơ P3, day3 19:00
DECLARE @S22 uniqueidentifier = NEWID(); -- Oppenheimer, Đồng Khởi Deluxe, day1 19:00, 3D
DECLARE @S23 uniqueidentifier = NEWID(); -- Spider-Verse, Đồng Khởi IMAX, day3 14:00, 3D, Special
DECLARE @S24 uniqueidentifier = NEWID(); -- Spider-Verse, Đà Nẵng P3, day5 09:00
DECLARE @S25 uniqueidentifier = NEWID(); -- Deadpool & Wolverine, Landmark81 P1, day4 21:30
DECLARE @S26 uniqueidentifier = NEWID(); -- Deadpool & Wolverine, Royal City Deluxe, day5 19:00, 3D
DECLARE @S27 uniqueidentifier = NEWID(); -- Wicked, Đồng Khởi P2, day5 14:00
DECLARE @S28 uniqueidentifier = NEWID(); -- Wicked, Cần Thơ P1, day2 09:00
DECLARE @S29 uniqueidentifier = NEWID(); -- Wicked, Landmark81 P2, day3 14:00
DECLARE @S30 uniqueidentifier = NEWID(); -- Bố Già, Đà Nẵng P1, day1 19:00
DECLARE @S31 uniqueidentifier = NEWID(); -- Bố Già, Cần Thơ P2, day4 14:00
DECLARE @S32 uniqueidentifier = NEWID(); -- Đào Phở và Piano, Royal City P1, day5 09:00
DECLARE @S33 uniqueidentifier = NEWID(); -- Đào Phở và Piano, Đồng Khởi P3, day2 21:30

INSERT INTO [ShowTime] (Id, MovieId, StartTime, EndTime, ProjectionForm, ShowTimeType, IsActive, CreationTime) VALUES
(@S1,  @M1,  DATEADD(hour,14,@Today),                     DATEADD(minute,@M1Dur,  DATEADD(hour,14,@Today)),                     1, 0, 1, GETUTCDATE()),
(@S2,  @M1,  DATEADD(minute,1290,DATEADD(day,1,@Today)),  DATEADD(minute,@M1Dur,  DATEADD(minute,1290,DATEADD(day,1,@Today))),  2, 0, 1, GETUTCDATE()),
(@S3,  @M1,  DATEADD(hour,19,DATEADD(day,2,@Today)),      DATEADD(minute,@M1Dur,  DATEADD(hour,19,DATEADD(day,2,@Today))),      1, 0, 1, GETUTCDATE()),
(@S4,  @M2,  DATEADD(hour,19,@Today),                     DATEADD(minute,@M2Dur,  DATEADD(hour,19,@Today)),                     1, 0, 1, GETUTCDATE()),
(@S5,  @M2,  DATEADD(hour,14,DATEADD(day,3,@Today)),      DATEADD(minute,@M2Dur,  DATEADD(hour,14,DATEADD(day,3,@Today))),      1, 0, 1, GETUTCDATE()),
(@S6,  @M2,  DATEADD(minute,1290,DATEADD(day,5,@Today)),  DATEADD(minute,@M2Dur,  DATEADD(minute,1290,DATEADD(day,5,@Today))),  1, 0, 1, GETUTCDATE()),
(@S7,  @M3,  DATEADD(hour,9,DATEADD(day,1,@Today)),       DATEADD(minute,@M3Dur,  DATEADD(hour,9,DATEADD(day,1,@Today))),       1, 0, 1, GETUTCDATE()),
(@S8,  @M3,  DATEADD(minute,1290,DATEADD(day,3,@Today)),  DATEADD(minute,@M3Dur,  DATEADD(minute,1290,DATEADD(day,3,@Today))),  2, 0, 1, GETUTCDATE()),
(@S9,  @M4,  DATEADD(hour,9,@Today),                      DATEADD(minute,@M4Dur,  DATEADD(hour,9,@Today)),                      1, 0, 1, GETUTCDATE()),
(@S10, @M4,  DATEADD(hour,19,DATEADD(day,2,@Today)),      DATEADD(minute,@M4Dur,  DATEADD(hour,19,DATEADD(day,2,@Today))),      1, 0, 1, GETUTCDATE()),
(@S11, @M4,  DATEADD(hour,14,DATEADD(day,4,@Today)),      DATEADD(minute,@M4Dur,  DATEADD(hour,14,DATEADD(day,4,@Today))),      1, 0, 1, GETUTCDATE()),
(@S12, @M5,  DATEADD(minute,1290,DATEADD(day,1,@Today)),  DATEADD(minute,@M5Dur,  DATEADD(minute,1290,DATEADD(day,1,@Today))),  1, 0, 1, GETUTCDATE()),
(@S13, @M5,  DATEADD(hour,19,DATEADD(day,4,@Today)),      DATEADD(minute,@M5Dur,  DATEADD(hour,19,DATEADD(day,4,@Today))),      1, 0, 1, GETUTCDATE()),
(@S14, @M5,  DATEADD(hour,9,DATEADD(day,3,@Today)),       DATEADD(minute,@M5Dur,  DATEADD(hour,9,DATEADD(day,3,@Today))),       1, 0, 1, GETUTCDATE()),
(@S15, @M6,  DATEADD(hour,9,DATEADD(day,2,@Today)),       DATEADD(minute,@M6Dur,  DATEADD(hour,9,DATEADD(day,2,@Today))),       1, 1, 1, GETUTCDATE()),
(@S16, @M6,  DATEADD(hour,14,@Today),                     DATEADD(minute,@M6Dur,  DATEADD(hour,14,@Today)),                     1, 0, 1, GETUTCDATE()),
(@S17, @M7,  DATEADD(hour,14,DATEADD(day,1,@Today)),      DATEADD(minute,@M7Dur,  DATEADD(hour,14,DATEADD(day,1,@Today))),      2, 0, 1, GETUTCDATE()),
(@S18, @M7,  DATEADD(minute,1290,DATEADD(day,4,@Today)),  DATEADD(minute,@M7Dur,  DATEADD(minute,1290,DATEADD(day,4,@Today))),  1, 0, 1, GETUTCDATE()),
(@S19, @M7,  DATEADD(hour,19,DATEADD(day,2,@Today)),      DATEADD(minute,@M7Dur,  DATEADD(hour,19,DATEADD(day,2,@Today))),      1, 0, 1, GETUTCDATE()),
(@S20, @M8,  DATEADD(minute,1290,@Today),                 DATEADD(minute,@M8Dur,  DATEADD(minute,1290,@Today)),                 1, 0, 1, GETUTCDATE()),
(@S21, @M8,  DATEADD(hour,19,DATEADD(day,3,@Today)),      DATEADD(minute,@M8Dur,  DATEADD(hour,19,DATEADD(day,3,@Today))),      1, 0, 1, GETUTCDATE()),
(@S22, @M8,  DATEADD(hour,19,DATEADD(day,1,@Today)),      DATEADD(minute,@M8Dur,  DATEADD(hour,19,DATEADD(day,1,@Today))),      1, 0, 1, GETUTCDATE()),
(@S23, @M9,  DATEADD(hour,14,DATEADD(day,3,@Today)),      DATEADD(minute,@M9Dur,  DATEADD(hour,14,DATEADD(day,3,@Today))),      2, 2, 1, GETUTCDATE()),
(@S24, @M9,  DATEADD(hour,9,DATEADD(day,5,@Today)),       DATEADD(minute,@M9Dur,  DATEADD(hour,9,DATEADD(day,5,@Today))),       1, 0, 1, GETUTCDATE()),
(@S25, @M10, DATEADD(minute,1290,DATEADD(day,4,@Today)),  DATEADD(minute,@M10Dur, DATEADD(minute,1290,DATEADD(day,4,@Today))),  1, 0, 1, GETUTCDATE()),
(@S26, @M10, DATEADD(hour,19,DATEADD(day,5,@Today)),      DATEADD(minute,@M10Dur, DATEADD(hour,19,DATEADD(day,5,@Today))),      1, 0, 1, GETUTCDATE()),
(@S27, @M11, DATEADD(hour,14,DATEADD(day,5,@Today)),      DATEADD(minute,@M11Dur, DATEADD(hour,14,DATEADD(day,5,@Today))),      1, 0, 1, GETUTCDATE()),
(@S28, @M11, DATEADD(hour,9,DATEADD(day,2,@Today)),       DATEADD(minute,@M11Dur, DATEADD(hour,9,DATEADD(day,2,@Today))),       1, 0, 1, GETUTCDATE()),
(@S29, @M11, DATEADD(hour,14,DATEADD(day,3,@Today)),      DATEADD(minute,@M11Dur, DATEADD(hour,14,DATEADD(day,3,@Today))),      1, 0, 1, GETUTCDATE()),
(@S30, @M12, DATEADD(hour,19,DATEADD(day,1,@Today)),      DATEADD(minute,@M12Dur, DATEADD(hour,19,DATEADD(day,1,@Today))),      1, 0, 1, GETUTCDATE()),
(@S31, @M12, DATEADD(hour,14,DATEADD(day,4,@Today)),      DATEADD(minute,@M12Dur, DATEADD(hour,14,DATEADD(day,4,@Today))),      1, 0, 1, GETUTCDATE()),
(@S32, @M13, DATEADD(hour,9,DATEADD(day,5,@Today)),       DATEADD(minute,@M13Dur, DATEADD(hour,9,DATEADD(day,5,@Today))),       1, 0, 1, GETUTCDATE()),
(@S33, @M13, DATEADD(minute,1290,DATEADD(day,2,@Today)),  DATEADD(minute,@M13Dur, DATEADD(minute,1290,DATEADD(day,2,@Today))),  1, 0, 1, GETUTCDATE());

-- ── 4. ShowTimeRoom ──
INSERT INTO [ShowTimeRoom] (ShowTimeId, RoomId, BasePrice) VALUES
(@S1,  @DK_P1,   85000),
(@S2,  @DK_IMAX, 130000),
(@S3,  @LM_P1,   85000),
(@S4,  @LM_P2,   85000),
(@S5,  @RC_P1,   80000),
(@S6,  @CT_P3,   75000),
(@S7,  @DK_P2,   85000),
(@S8,  @LM_4DX,  135000),
(@S9,  @DK_P3,   85000),
(@S10, @RC_DLX,  125000),
(@S11, @CT_P1,   75000),
(@S12, @RC_P2,   80000),
(@S13, @DN_P1,   80000),
(@S14, @DK_P2,   85000),
(@S15, @DK_P1,   90000),
(@S16, @CT_P2,   75000),
(@S17, @LM_4DX,  140000),
(@S18, @DK_DLX,  130000),
(@S19, @DN_P2,   80000),
(@S20, @RC_P3,   80000),
(@S21, @CT_P3,   75000),
(@S22, @DK_DLX,  130000),
(@S23, @DK_IMAX, 135000),
(@S24, @DN_P3,   80000),
(@S25, @LM_P1,   85000),
(@S26, @RC_DLX,  130000),
(@S27, @DK_P2,   85000),
(@S28, @CT_P1,   75000),
(@S29, @LM_P2,   85000),
(@S30, @DN_P1,   80000),
(@S31, @CT_P2,   75000),
(@S32, @RC_P1,   80000),
(@S33, @DK_P3,   85000);

GO

-- ── Part E: User accounts ──────────────────────────────────────────────────
-- Admin/Customer here use the SAME email+phone as Cinema.Business.Tests' seeding exe
-- (admin@cinema.vn/Admin@123, user@cinema.vn/User@123) so running that exe afterward
-- sees the accounts already exist (its GetByEmailAsync check) and just skips them —
-- no duplicate-email conflict either way. PasswordHash/PasswordSalt below are real
-- PBKDF2-SHA256 (100,000 iterations, 32-byte output) hashes of the passwords in each
-- comment, generated with the exact same parameters as AuthManager/CreatePasswordHash.
DECLARE @UT_Admin    uniqueidentifier = (SELECT Id FROM [UserType] WHERE Name = N'Admin');
DECLARE @UT_Customer uniqueidentifier = (SELECT Id FROM [UserType] WHERE Name = N'Customer');
DECLARE @MS_Bronze   uniqueidentifier = (SELECT Id FROM [MemberShip] WHERE Name = N'Hạng Đồng');
DECLARE @MS_Silver   uniqueidentifier = (SELECT Id FROM [MemberShip] WHERE Name = N'Hạng Bạc');
DECLARE @MS_Gold     uniqueidentifier = (SELECT Id FROM [MemberShip] WHERE Name = N'Hạng Vàng');

INSERT INTO [User]
    ([Id], [Phone], [Email], [Name], [PasswordHash], [PasswordSalt], [Status],
     [UserTypeId], [MemberShipId], [Points], [EmailConfirmed], [CreationTime])
VALUES
-- Admin@123
(NEWID(), N'0900000000', N'admin@cinema.vn', N'Admin',
    0x41c77d64248f6d3a23f307a675eb582cf5587056b362bc7fc44a677b4fa66628,
    0x662e3464f2ea4180ca21d2a35b8943bb,
    0, @UT_Admin, NULL, 0, 1, GETUTCDATE()),
-- User@123
(NEWID(), N'0900000001', N'user@cinema.vn', N'User',
    0xc7bb6360387601762e2ef3f5925f4affd3c878a6073c05866e2e8e6fa78029e5,
    0x4363c06ea853206dee339359cbdbbae9,
    0, @UT_Customer, NULL, 0, 1, GETUTCDATE()),
-- Khach01@123
(NEWID(), N'0901111111', N'khach01@cinema.vn', N'Nguyễn Văn An',
    0xb7897668dfb4c1d86b4a069487c0b823578fc1b2020793b9ce8378df4f9573b4,
    0x1ad62b359f5bac7f1d2f639c5e5e0382,
    0, @UT_Customer, @MS_Silver, 1500, 1, GETUTCDATE()),
-- Khach02@123
(NEWID(), N'0901111112', N'khach02@cinema.vn', N'Trần Thị Bình',
    0x6b2c01fcb7073fedf3393aff4d92409c91d9e61cf3ce64c70b3c0dd0f5cd79a4,
    0xacc2f88f47aca7f9cb3e775387b787f8,
    0, @UT_Customer, @MS_Gold, 6000, 1, GETUTCDATE()),
-- Khach03@123
(NEWID(), N'0901111113', N'khach03@cinema.vn', N'Lê Hoàng Cường',
    0xdc0ffc3cfb98dc604123601afbdb1dc190d634acb3c3b04cdfa3a9be04eb8bc2,
    0x207cda702cf9ae21f6b34108baaa9eb0,
    0, @UT_Customer, @MS_Bronze, 200, 1, GETUTCDATE());
