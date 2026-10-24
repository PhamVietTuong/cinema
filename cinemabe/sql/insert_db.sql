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
(@UserTypeStaff,    N'TheaterStaff', GETUTCDATE());

-- ── Membership tiers ─────────────────────────────────────────────────────────
DECLARE @MemberBronze   uniqueidentifier = NEWID();
DECLARE @MemberSilver   uniqueidentifier = NEWID();
DECLARE @MemberGold     uniqueidentifier = NEWID();
DECLARE @MemberPlatinum uniqueidentifier = NEWID();
DECLARE @MemberDiamond  uniqueidentifier = NEWID();

INSERT INTO [MemberShip] ([Id], [Name], [MinPoints], [MaxPoints], [DiscountPercent], [CreationTime]) VALUES
(@MemberBronze,   N'Hạng Đồng',        0,     999,   0,  GETUTCDATE()),
(@MemberSilver,   N'Hạng Bạc',         1000,  4999,  5,  GETUTCDATE()),
(@MemberGold,     N'Hạng Vàng',        5000,  9999,  10, GETUTCDATE()),
(@MemberPlatinum, N'Hạng Bạch Kim',    10000, 19999, 12, GETUTCDATE()),
(@MemberDiamond,  N'Hạng Kim Cương',   20000, 99999, 15, GETUTCDATE());

-- ── Loyalty points (global default rate; theaters may override in LoyaltySetting) ──
INSERT INTO [LoyaltySetting] ([Id], [TheaterId], [PointsPerUnit], [PointValueVnd], [CreationTime]) VALUES
(NEWID(), NULL, 1000, 1000, GETUTCDATE());

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
(@Theater5, N'Cinema Sense City Cần Thơ', N'1 Đại Lộ Hòa Bình, Phường Tân An, Ninh Kiều',            N'Cần Thơ',         N'0292-3812-345', N'cantho@cinema.vn',     1, 10.0341, 105.7881, GETUTCDATE()),
-- Flagship (large multiplex) theaters — also get LoyaltySetting overrides and the biggest room fan-out
(NEWID(), N'Cinema Vạn Hạnh Mall',        N'11 Sư Vạn Hạnh, Phường 12, Quận 10',                     N'TP. Hồ Chí Minh', N'028-3863-5001', N'vanhanh@cinema.vn',    1, 10.7726, 106.6675, GETUTCDATE()),
(NEWID(), N'Cinema Times City',           N'458 Minh Khai, Phường Vĩnh Tuy, Hai Bà Trưng',            N'Hà Nội',          N'024-3633-5002', N'timescity@cinema.vn', 1, 20.9947, 105.8664, GETUTCDATE()),
(NEWID(), N'Cinema Lotte Đà Nẵng',        N'6 Nại Nam, Phường Hòa Cường Bắc, Hải Châu',               N'Đà Nẵng',         N'0236-3999-503', N'lottedanang@cinema.vn', 1, 16.0322, 108.2135, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Nha Trang',     N'60 Trần Phú, Phường Lộc Thọ, Nha Trang',                  N'Nha Trang',       N'0258-3523-504', N'nhatrang@cinema.vn',   1, 12.2461, 109.1967, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Hải Phòng',     N'1 Nguyễn Đức Cảnh, Phường Trần Thành Ngọ, Kiến An',       N'Hải Phòng',       N'0225-3822-505', N'haiphong1@cinema.vn', 1, 20.8386, 106.6450, GETUTCDATE()),
-- Standard theaters
(NEWID(), N'Cinema Crescent Mall',        N'101 Tôn Dật Tiên, Phường Tân Phú, Quận 7',                N'TP. Hồ Chí Minh', N'028-5413-5006', N'crescent@cinema.vn',  1, 10.7293, 106.7188, GETUTCDATE()),
(NEWID(), N'Cinema Aeon Tân Phú',         N'30 Bờ Bao Tân Thắng, Phường Sơn Kỳ, Tân Phú',             N'TP. Hồ Chí Minh', N'028-3810-5007', N'aeontanphu@cinema.vn', 1, 10.8010, 106.6191, GETUTCDATE()),
(NEWID(), N'Cinema Aeon Long Biên',       N'27 Cổ Linh, Phường Long Biên, Long Biên',                 N'Hà Nội',          N'024-3652-5008', N'longbien@cinema.vn',  1, 21.0432, 105.8980, GETUTCDATE()),
(NEWID(), N'Cinema Lotte Đống Đa',        N'229 Tây Sơn, Phường Ngã Tư Sở, Đống Đa',                  N'Hà Nội',          N'024-3537-5009', N'dongda@cinema.vn',    1, 21.0075, 105.8198, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Huế',           N'50A Hùng Vương, Phường Phú Nhuận, Huế',                   N'Huế',             N'0234-3939-510', N'hue@cinema.vn',       1, 16.4598, 107.5911, GETUTCDATE()),
(NEWID(), N'Cinema Lotte Vũng Tàu',       N'2 Lê Hồng Phong, Phường Thắng Tam, Vũng Tàu',             N'Vũng Tàu',        N'0254-3511-511', N'vungtau@cinema.vn',   1, 10.3490, 107.0843, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Biên Hòa',      N'1096 Phạm Văn Thuận, Phường Tam Hiệp, Biên Hòa',          N'Biên Hòa',        N'0251-3822-512', N'bienhoa@cinema.vn',   1, 10.9447, 106.8512, GETUTCDATE()),
-- Compact theaters
(NEWID(), N'Cinema Gigamall Thủ Đức',     N'242 Phạm Văn Đồng, Phường Hiệp Bình Chánh, Thủ Đức',      N'TP. Hồ Chí Minh', N'028-6682-5013', N'gigamall@cinema.vn',  1, 10.8487, 106.7225, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Bà Triệu',      N'191 Bà Triệu, Phường Lê Đại Hành, Hai Bà Trưng',          N'Hà Nội',          N'024-3974-5014', N'batrieu@cinema.vn',   1, 21.0091, 105.8481, GETUTCDATE()),
(NEWID(), N'Cinema Aeon Bình Dương',      N'1 Đại lộ Bình Dương, Phường Hòa Phú, Thủ Dầu Một',        N'Bình Dương',      N'0274-3822-515', N'binhduong@cinema.vn', 1, 10.9804, 106.6519, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Quy Nhơn',      N'01 Ngô Mây, Phường Nguyễn Văn Cừ, Quy Nhơn',              N'Quy Nhơn',        N'0256-3822-516', N'quynhon@cinema.vn',   1, 13.7757, 109.2238, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Buôn Ma Thuột', N'72-74 Điểu Núp, Phường Tân Lợi, Buôn Ma Thuột',           N'Buôn Ma Thuột',   N'0262-3822-517', N'bmt@cinema.vn',       1, 12.6667, 108.0500, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Đà Lạt',        N'1 Phù Đổng Thiên Vương, Phường 8, Đà Lạt',                N'Đà Lạt',          N'0263-3822-518', N'dalat@cinema.vn',     1, 11.9404, 108.4583, GETUTCDATE()),
(NEWID(), N'Cinema Vincom Hạ Long',       N'18 Hạ Long, Phường Bãi Cháy, Hạ Long',                    N'Hạ Long',         N'0203-3822-519', N'halong@cinema.vn',    1, 20.9519, 107.0796, GETUTCDATE()),
(NEWID(), N'Cinema Aeon Hải Phòng',       N'10 Lê Hồng Phong, Phường Đông Khê, Ngô Quyền',            N'Hải Phòng',       N'0225-3822-520', N'haiphong2@cinema.vn', 1, 20.8585, 106.6906, GETUTCDATE());

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
INSERT INTO [PatronCategory] ([Id], [TheaterId], [SeatTypeId], [Name], [Price], [IsActive], [RequiresIdCheck], [CreationTime])
SELECT NEWID(), t.Id, st.Id, c.Name, c.Price, 1, c.RequiresIdCheck, GETUTCDATE()
FROM [Theater] t
JOIN [SeatType] st ON st.TheaterId = t.Id
CROSS JOIN (VALUES
    (N'Người Lớn',              0, 90000,  0),
    (N'Người Lớn',              1, 170000, 0),
    (N'Học Sinh - Sinh Viên',   0, 65000,  1),
    (N'Người Cao Tuổi',         0, 60000,  1),
    (N'Người Cao Tuổi',         1, 115000, 1),
    (N'Trẻ Em',                 0, 50000,  0)
) AS c(Name, Kind, Price, RequiresIdCheck)
WHERE st.Kind = c.Kind;

-- ── Food & drinks (per theater) ───────────────────────────────────────────────
INSERT INTO [FoodAndDrink] ([Id], [TheaterId], [Name], [Price], [Description], [ImageUrl], [IsAvailable], [CreationTime])
SELECT NEWID(), t.Id, f.Name, f.Price, f.Description, f.ImageUrl, f.IsAvailable, GETUTCDATE()
FROM [Theater] t
CROSS JOIN (VALUES
    (N'Bắp Rang Thường',       35000, N'Bắp rang bơ hoặc mặn 500ml',                          N'https://upload.wikimedia.org/wikipedia/commons/e/e2/Cinema_popcorn_bucket.jpg',                                              1),
    (N'Bắp Rang Lớn',          55000, N'Bắp rang bơ hoặc mặn 1000ml',                          N'https://upload.wikimedia.org/wikipedia/commons/4/42/Regal_Cinemas_popcorn_bucket.jpg',                                       1),
    (N'Coca-Cola',             30000, N'Coca-Cola lạnh 500ml',                                 N'https://upload.wikimedia.org/wikipedia/commons/2/2f/Coca-cola_50cl_can_-_Italia.jpg',                                        1),
    (N'Pepsi',                 30000, N'Pepsi lạnh 500ml',                                     N'https://upload.wikimedia.org/wikipedia/commons/b/b6/Pepsi_Max_can.jpg',                                                      1),
    (N'Combo Bắp Nước',        75000, N'Bắp rang thường kèm 1 nước ngọt 500ml',                N'https://upload.wikimedia.org/wikipedia/commons/e/ed/Popcorn%21%21_%282521342015%29.jpg',                                     1),
    (N'Nachos Phô Mai',        45000, N'Bánh nachos kèm sốt phô mai 200g',                     N'https://upload.wikimedia.org/wikipedia/commons/7/78/Nachos_with_Melted_Cheese%2C_Jalapenos%2C_and_Tasty_Toppings.jpg',        1),
    (N'Hot Dog',               40000, N'Hot dog kiểu Mỹ kèm mù tạt và tương cà',                N'https://upload.wikimedia.org/wikipedia/commons/a/ad/Hot_dog_01.jpg',                                                         1),
    (N'Bắp Rang Caramel',      45000, N'Bắp rang phủ caramel ngọt 500ml',                       N'https://upload.wikimedia.org/wikipedia/commons/9/9b/Caramel_Popcorn_%2832402585090%29.jpg',                                  1),
    (N'Trà Đào Cam Sả',        42000, N'Trà đào cam sả mát lạnh 500ml',                         N'https://upload.wikimedia.org/wikipedia/commons/6/6b/Peach_iced_tea_with_orange_slices.jpg',                                  1),
    (N'Snack Khoai Tây',       32000, N'Khoai tây chiên giòn phần lớn',                          N'https://upload.wikimedia.org/wikipedia/commons/1/10/Opened_bag_of_Ruffles_All_Dressed_potato_chips.jpg',                     1),
    (N'Kẹo Dẻo Trái Cây',      25000, N'Kẹo dẻo hương trái cây tổng hợp 150g',                  N'https://upload.wikimedia.org/wikipedia/commons/f/fa/Gummy_Bear.jpg',                                                         1),
    (N'Combo Gia Đình',       150000, N'2 bắp rang lớn kèm 4 nước ngọt 500ml',                  N'https://upload.wikimedia.org/wikipedia/commons/c/ce/Caramel_Chocolate_Popcorn_in_a_bucket_%2815256400336%29.jpg',            1),
    (N'Bắp Rang Phô Mai',      48000, N'Bắp rang phủ sốt phô mai cheddar 500ml',                N'https://picsum.photos/seed/bap-rang-pho-mai/400/300',                                                                        1),
    (N'Nước Suối',             20000, N'Nước suối tinh khiết 500ml',                            N'https://picsum.photos/seed/nuoc-suoi/400/300',                                                                                1),
    (N'Trà Sữa Trân Châu',     45000, N'Trà sữa trân châu đường đen 500ml',                     N'https://picsum.photos/seed/tra-sua-tran-chau/400/300',                                                                        1),
    (N'Xúc Xích Nướng',        38000, N'Xúc xích nướng kiểu Đức kèm tương ớt',                  N'https://picsum.photos/seed/xuc-xich-nuong/400/300',                                                                           1),
    (N'Combo Solo',            65000, N'1 bắp rang thường kèm 1 nước suối',                     N'https://upload.wikimedia.org/wikipedia/commons/e/ed/Popcorn%21%21_%282521342015%29.jpg',                                     1),
    (N'Combo Đôi',            135000, N'1 bắp rang lớn kèm 2 nước ngọt và 1 nachos phô mai',    N'https://upload.wikimedia.org/wikipedia/commons/c/ce/Caramel_Chocolate_Popcorn_in_a_bucket_%2815256400336%29.jpg',            1)
) AS f(Name, Price, Description, ImageUrl, IsAvailable);

-- ── Seeded combos: flag the combo items and give each a recipe (tracking stays off) ──
UPDATE [FoodAndDrink] SET [IsCombo] = 1 WHERE [Name] IN (N'Combo Bắp Nước', N'Combo Gia Đình', N'Combo Solo', N'Combo Đôi');

INSERT INTO [ComboItem] ([Id], [ComboId], [ComponentId], [Quantity], [CreationTime])
SELECT NEWID(), combo.Id, comp.Id, r.Quantity, GETUTCDATE()
FROM (VALUES
    (N'Combo Bắp Nước', N'Bắp Rang Thường', 1),
    (N'Combo Bắp Nước', N'Coca-Cola',       1),
    (N'Combo Gia Đình', N'Bắp Rang Lớn',    2),
    (N'Combo Gia Đình', N'Coca-Cola',       4),
    (N'Combo Solo',     N'Bắp Rang Thường', 1),
    (N'Combo Solo',     N'Nước Suối',       1),
    (N'Combo Đôi',      N'Bắp Rang Lớn',    1),
    (N'Combo Đôi',      N'Pepsi',           2),
    (N'Combo Đôi',      N'Nachos Phô Mai',  1)
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

-- ── Ticket prices (time-of-day/day-of-week multiplier per theater/room type/time slot) ──
-- Holiday pricing is not seeded here: it comes from the Holiday table by calendar date.
INSERT INTO [TicketPrice] ([Id], [TheaterId], [RoomTypeId], [TimeSlotId], [DayOfWeek], [PriceMultiplier], [CreationTime])
SELECT NEWID(), rt.TheaterId, rt.Id, ts.Id, d.DayOfWeek,
    (CASE ts.Name WHEN N'Tối' THEN 1.2 WHEN N'Khuya' THEN 1.3 WHEN N'Sáng' THEN 0.9 ELSE 1.0 END)
    * (CASE rt.Name WHEN N'IMAX' THEN 1.5 WHEN N'4DX' THEN 1.8 WHEN N'Deluxe' THEN 1.4 WHEN N'3D' THEN 1.2 ELSE 1.0 END),
    GETUTCDATE()
FROM [RoomType] rt
JOIN [TimeSlot] ts ON ts.TheaterId = rt.TheaterId
CROSS JOIN (VALUES (0), (1), (2), (3), (4), (5), (6)) AS d(DayOfWeek);

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

-- ── Loyalty overrides for the 5 flagship theaters (global default row stays for everyone else) ──
INSERT INTO [LoyaltySetting] ([Id], [TheaterId], [PointsPerUnit], [PointValueVnd], [CreationTime])
SELECT NEWID(), t.Id, l.PointsPerUnit, l.PointValueVnd, GETUTCDATE()
FROM [Theater] t
JOIN (VALUES
    (N'Cinema Vạn Hạnh Mall',    800, 1000),
    (N'Cinema Times City',       800, 1000),
    (N'Cinema Lotte Đà Nẵng',    500, 1000),
    (N'Cinema Vincom Nha Trang', 500, 1000),
    (N'Cinema Vincom Hải Phòng', 500, 1000)
) AS l(TheaterName, PointsPerUnit, PointValueVnd) ON l.TheaterName = t.Name;

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
-- (pair ids declared up front — NEWID() inside a VALUES row that's read by both
-- matching cross-join columns gets re-evaluated per output row, not once per source row)
DECLARE @T1_R3_PairA uniqueidentifier = NEWID();
DECLARE @T1_R3_PairB uniqueidentifier = NEWID();
DECLARE @T1_R3_PairC uniqueidentifier = NEWID();
DECLARE @T1_R3_PairD uniqueidentifier = NEWID();
DECLARE @T1_R3_PairE uniqueidentifier = NEWID();
DECLARE @T1_R3_PairF uniqueidentifier = NEWID();

INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[SeatGroupId],[IsActive],[CreationTime])
SELECT NEWID(), @T1_R3, r.RowLetter, c.ColNum,
       CASE WHEN c.ColNum IN (9,10) THEN r.PairGroupId ELSE NULL END, 1, GETUTCDATE()
FROM (VALUES ('A',@T1_R3_PairA),('B',@T1_R3_PairB),('C',@T1_R3_PairC),('D',@T1_R3_PairD),('E',@T1_R3_PairE),('F',@T1_R3_PairF)) AS r(RowLetter, PairGroupId)
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

-- ── Part C2: Rooms + Seats for the 20 additional theaters, set-based (no per-room DECLAREs) ──
DROP TABLE IF EXISTS #RoomGen;
DROP TABLE IF EXISTS #Pairs;

CREATE TABLE #RoomGen (
    Id         uniqueidentifier NOT NULL DEFAULT NEWID(),
    TheaterId  uniqueidentifier NOT NULL,
    RoomTypeId uniqueidentifier NOT NULL,
    Name       nvarchar(100)    NOT NULL,
    Rows       int              NOT NULL,
    Cols       int              NOT NULL
);

-- Theater → room profile (F = flagship, S = standard, C = compact)
INSERT INTO #RoomGen (TheaterId, RoomTypeId, Name, Rows, Cols)
SELECT t.Id, rt.Id, tmpl.RoomName, tmpl.Rows, tmpl.Cols
FROM (VALUES
    (N'Cinema Vạn Hạnh Mall',        N'F'),
    (N'Cinema Times City',           N'F'),
    (N'Cinema Lotte Đà Nẵng',        N'F'),
    (N'Cinema Vincom Nha Trang',     N'F'),
    (N'Cinema Vincom Hải Phòng',     N'F'),
    (N'Cinema Crescent Mall',        N'S'),
    (N'Cinema Aeon Tân Phú',         N'S'),
    (N'Cinema Aeon Long Biên',       N'S'),
    (N'Cinema Lotte Đống Đa',        N'S'),
    (N'Cinema Vincom Huế',           N'S'),
    (N'Cinema Lotte Vũng Tàu',       N'S'),
    (N'Cinema Vincom Biên Hòa',      N'S'),
    (N'Cinema Gigamall Thủ Đức',     N'C'),
    (N'Cinema Vincom Bà Triệu',      N'C'),
    (N'Cinema Aeon Bình Dương',      N'C'),
    (N'Cinema Vincom Quy Nhơn',      N'C'),
    (N'Cinema Vincom Buôn Ma Thuột', N'C'),
    (N'Cinema Vincom Đà Lạt',        N'C'),
    (N'Cinema Vincom Hạ Long',       N'C'),
    (N'Cinema Aeon Hải Phòng',       N'C')
) AS m(TheaterName, Profile)
JOIN [Theater] t ON t.Name = m.TheaterName
JOIN (VALUES
    (N'F', N'Phòng 1',      N'2D',     10, 14),
    (N'F', N'Phòng 2',      N'2D',     9,  12),
    (N'F', N'Phòng 3',      N'3D',     9,  12),
    (N'F', N'Phòng IMAX',   N'IMAX',   12, 16),
    (N'F', N'Phòng 4DX',    N'4DX',    8,  10),
    (N'F', N'Phòng Deluxe', N'Deluxe', 6,  8),
    (N'S', N'Phòng 1',      N'2D',     9,  12),
    (N'S', N'Phòng 2',      N'2D',     8,  12),
    (N'S', N'Phòng 3',      N'3D',     8,  12),
    (N'S', N'Phòng 4',      N'2D',     7,  10),
    (N'S', N'Phòng Deluxe', N'Deluxe', 6,  8),
    (N'C', N'Phòng 1',      N'2D',     8,  12),
    (N'C', N'Phòng 2',      N'2D',     8,  10),
    (N'C', N'Phòng 3',      N'3D',     8,  12),
    (N'C', N'Phòng 4',      N'2D',     6,  10)
) AS tmpl(Profile, RoomName, TypeName, Rows, Cols) ON tmpl.Profile = m.Profile
JOIN [RoomType] rt ON rt.TheaterId = t.Id AND rt.Name = tmpl.TypeName
WHERE NOT EXISTS (SELECT 1 FROM [Room] r WHERE r.TheaterId = t.Id);

INSERT INTO [Room] ([Id],[Name],[TheaterId],[RoomTypeId],[TotalRows],[TotalColumns],[Status],[CreationTime])
SELECT Id, Name, TheaterId, RoomTypeId, Rows, Cols, 0, GETUTCDATE()
FROM #RoomGen;

-- Seats for every generated room (row letters A..L, columns 1..16, capped per room by Rows/Cols)
INSERT INTO [Seat] ([Id],[RoomId],[RowName],[ColIndex],[IsActive],[CreationTime])
SELECT NEWID(), g.Id, rl.RowLetter, c.ColNum, 1, GETUTCDATE()
FROM #RoomGen g
CROSS JOIN (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8),('I',9),('J',10),('K',11),('L',12)) AS rl(RowLetter, RowIdx)
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14),(15),(16)) AS c(ColNum)
WHERE rl.RowIdx <= g.Rows AND c.ColNum <= g.Cols;

-- Couple seats: every pair of columns in the last row of each 2D/3D/Deluxe generated room
CREATE TABLE #Pairs (
    RoomId  uniqueidentifier NOT NULL,
    RowName nvarchar(5)      NOT NULL,
    PairNo  int              NOT NULL,
    GroupId uniqueidentifier NOT NULL DEFAULT NEWID()
);

INSERT INTO #Pairs (RoomId, RowName, PairNo)
SELECT g.Id, lr.RowLetter, pn.PairNo
FROM #RoomGen g
JOIN [RoomType] rt ON rt.Id = g.RoomTypeId
CROSS APPLY (
    SELECT RowLetter FROM (VALUES ('A',1),('B',2),('C',3),('D',4),('E',5),('F',6),('G',7),('H',8),('I',9),('J',10),('K',11),('L',12)) AS x(RowLetter, RowIdx)
    WHERE x.RowIdx = g.Rows
) AS lr
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) AS pn(PairNo)
WHERE rt.Name IN (N'2D', N'3D', N'Deluxe') AND pn.PairNo <= g.Cols / 2;

UPDATE s
SET s.SeatGroupId = p.GroupId
FROM [Seat] s
JOIN #RoomGen g ON g.Id = s.RoomId
JOIN #Pairs p ON p.RoomId = s.RoomId AND p.RowName = s.RowName AND p.PairNo = (s.ColIndex + 1) / 2;

DROP TABLE #Pairs;
DROP TABLE #RoomGen;

GO

-- ── Part D: Movies, Genres (the 18 hand-typed movies; ShowTimes come from Part D3) ──

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

-- ── Movie Ids + Durations (declared up front, reused for MovieTypeDetail) ──
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
INSERT INTO [Movie] (Id, Title, Slug, Description, Duration, ReleaseDate, EndDate, PosterUrl, TrailerUrl, Director, Cast, Language, Subtitle, IsActive, AgeRestrictionId, CreationTime)
VALUES
(@M1, N'Inception', N'inception', N'Một tên trộm chuyên đánh cắp bí mật thông qua công nghệ chia sẻ giấc mơ được giao nhiệm vụ ngược lại: gieo một ý tưởng vào tâm trí mục tiêu.', @M1Dur, DATEADD(day,-120,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg', N'https://www.youtube.com/watch?v=YoHD9XEInc0', N'Christopher Nolan', N'Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M2, N'The Dark Knight', N'the-dark-knight', N'Batman đối đầu với Joker, tên tội phạm điên loạn muốn đẩy Gotham vào hỗn loạn, buộc anh phải đối mặt với những giới hạn đạo đức của chính mình.', @M2Dur, DATEADD(day,-200,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/qJ2tW6WMUDux911r6m7haRef0WH.jpg', N'https://www.youtube.com/watch?v=EXeTwQWrcwY', N'Christopher Nolan', N'Christian Bale, Heath Ledger, Aaron Eckhart', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M3, N'Interstellar', N'interstellar', N'Một nhóm phi hành gia du hành qua hố sâu vũ trụ để tìm kiếm ngôi nhà mới cho nhân loại khi Trái Đất đang dần trở nên không thể sinh sống.', @M3Dur, DATEADD(day,-60,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg', N'https://www.youtube.com/watch?v=zSWdZVtXT7E', N'Christopher Nolan', N'Matthew McConaughey, Anne Hathaway, Jessica Chastain', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M4, N'Avengers: Endgame', N'avengers-endgame', N'Các siêu anh hùng còn sống sót tập hợp để đảo ngược thiệt hại mà Thanos đã gây ra, trong trận chiến cuối cùng quyết định vận mệnh vũ trụ.', @M4Dur, DATEADD(day,-30,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/or06FN3Dka5tukK1e9sl16pB3iy.jpg', N'https://www.youtube.com/watch?v=TcMBFSGVi1c', N'Anthony Russo, Joe Russo', N'Robert Downey Jr., Chris Evans, Scarlett Johansson', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M5, N'Parasite', N'parasite', N'Câu chuyện về gia đình nghèo khó tìm cách len lỏi vào cuộc sống của một gia đình giàu có, dẫn đến những hệ lụy bất ngờ và đầy ám ảnh.', @M5Dur, DATEADD(day,-90,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg', N'https://www.youtube.com/watch?v=5xH0HfJHsaY', N'Bong Joon-ho', N'Song Kang-ho, Lee Sun-kyun, Cho Yeo-jeong', N'Tiếng Hàn', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M6, N'The Lion King', N'the-lion-king', N'Chú sư tử con Simba phải trưởng thành và đối mặt với quá khứ để giành lại ngôi vị vua của vùng đất tổ tiên.', @M6Dur, DATEADD(day,-15,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/hNltEgOaOIONA6peqiIb9WR1lTG.jpg', N'https://www.youtube.com/watch?v=7TavVZMewpY', N'Jon Favreau', N'Donald Glover, Beyoncé, Chiwetel Ejiofor', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeP, GETUTCDATE()),
(@M7, N'Top Gun: Maverick', N'top-gun-maverick', N'Phi công huyền thoại Maverick trở lại huấn luyện thế hệ phi công mới cho một nhiệm vụ liều lĩnh bậc nhất sự nghiệp.', @M7Dur, DATEADD(day,-45,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/62HCnUTziyWcpDaBO2i1DX17ljH.jpg', N'https://www.youtube.com/watch?v=giXco2jaZ_4', N'Joseph Kosinski', N'Tom Cruise, Miles Teller, Jennifer Connelly', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M8, N'Oppenheimer', N'oppenheimer', N'Câu chuyện về nhà vật lý J. Robert Oppenheimer và vai trò của ông trong việc phát triển bom nguyên tử, cùng những giằng xé đạo đức sau đó.', @M8Dur, DATEADD(day,-70,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/8Gxv8gSFCU0XGDykEGv7zR1n2ua.jpg', N'https://www.youtube.com/watch?v=uYPbbksJxIg', N'Christopher Nolan', N'Cillian Murphy, Emily Blunt, Matt Damon', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M9, N'Spider-Man: Across the Spider-Verse', N'spider-man-across-the-spider-verse', N'Miles Morales du hành qua đa vũ trụ cùng Gwen Stacy, đối mặt với một phản diện mới và số phận đầy tranh cãi của chính mình.', @M9Dur, DATEADD(day,-25,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/paM6UdMgXuXyAK0jhGfV07o3lRW.jpg', N'https://www.youtube.com/watch?v=cqGjhVJWtEg', N'Joaquim Dos Santos, Kemp Powers', N'Shameik Moore, Hailee Steinfeld', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeK, GETUTCDATE()),
(@M10, N'Deadpool & Wolverine', N'deadpool-wolverine', N'Deadpool bắt tay cùng Wolverine trong một hành trình hài hước, bạo lực và đầy bất ngờ xuyên suốt đa vũ trụ Marvel.', @M10Dur, DATEADD(day,-10,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/8cdWjvZQUExUUTzyp4t6EDMubfO.jpg', N'https://www.youtube.com/watch?v=73_1biulkYk', N'Shawn Levy', N'Ryan Reynolds, Hugh Jackman', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT18, GETUTCDATE()),
(@M11, N'Wicked', N'wicked', N'Câu chuyện về tình bạn giữa Elphaba và Glinda trước khi một người trở thành Phù thủy Xứ Oz, kể lại qua âm nhạc đầy màu sắc.', @M11Dur, DATEADD(day,-5,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/v0wmLuNj4rsyfTF6V30StYZAXdb.jpg', N'https://www.youtube.com/watch?v=6COmYeLsyc8', N'Jon M. Chu', N'Cynthia Erivo, Ariana Grande', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeK, GETUTCDATE()),
(@M12, N'Bố Già', N'bo-gia', N'Bộ phim hài - chính kịch xoay quanh những mâu thuẫn và tình cảm gia đình trong một con hẻm nhỏ ở Sài Gòn.', @M12Dur, DATEADD(day,-80,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/WoFe2UXDzLAZAk6aFxcOkBXyfT.jpg', N'https://www.youtube.com/watch?v=EFbdOSHzhtA', N'Trấn Thành, Vũ Ngọc Đãng', N'Trấn Thành, Tuấn Trần, Ngân Chi', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M13, N'Đào, Phở và Piano', N'dao-pho-va-piano', N'Bộ phim tái hiện những ngày cuối cùng của trận chiến Hà Nội mùa đông 1946, khắc họa tình yêu và lòng quả cảm giữa khói lửa chiến tranh.', @M13Dur, DATEADD(day,-40,CAST(GETUTCDATE() AS date)), DATEADD(month,6,GETUTCDATE()), N'https://image.tmdb.org/t/p/original/jWYfucBwXG3ePh97bM5ReT1q65X.jpg', N'https://www.youtube.com/watch?v=1fGh6vXwCsE', N'Phi Tiến Sơn', N'Doãn Quốc Đam, Cao Thị Thùy Linh', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M14, N'Dune: Part Two', N'dune-part-two', N'Paul Atreides tiếp tục hành trình trả thù và định mệnh của mình trên hành tinh sa mạc Arrakis, dẫn dắt người Fremen chống lại nhà Harkonnen.', @M14Dur, DATEADD(month,2,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/1pdfLvkbY9ohJlCjQH2CZjjYVvJ.jpg', N'https://www.youtube.com/watch?v=Way9Dexny3w', N'Denis Villeneuve', N'Timothée Chalamet, Zendaya, Rebecca Ferguson', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT13, GETUTCDATE()),
(@M15, N'Inside Out 2', N'inside-out-2', N'Riley bước vào tuổi dậy thì và phải học cách đối diện với những cảm xúc mới xuất hiện trong tâm trí, bên cạnh những cảm xúc quen thuộc.', @M15Dur, DATEADD(month,1,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/vpnVM9B6NMmQpWeZvzLvDESb2QY.jpg', N'https://www.youtube.com/watch?v=LEjhY15eCx0', N'Kelsey Mann', N'Amy Poehler, Maya Hawke', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeP, GETUTCDATE()),
(@M16, N'A Quiet Place: Day One', N'a-quiet-place-day-one', N'Câu chuyện tiền truyện kể về ngày đầu tiên loài quái vật săn mồi bằng âm thanh xâm chiếm Trái Đất, qua góc nhìn của những người sống sót đầu tiên.', @M16Dur, DATEADD(month,3,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/bi1YGU4vvUvoXvzbN0E7RRSNlMQ.jpg', N'https://www.youtube.com/watch?v=Xzt2P1sxaHY', N'Michael Sarnoski', N'Lupita Nyong''o, Joseph Quinn', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M17, N'Mai', N'mai', N'Bộ phim chính kịch - tình cảm kể về hành trình tìm kiếm hạnh phúc của một người phụ nữ với quá khứ đầy tổn thương.', @M17Dur, DATEADD(month,1,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/2nF8xD200rcDawuCg5ObxxqA2fC.jpg', N'https://www.youtube.com/watch?v=DVfLJFHVTdI', N'Trấn Thành', N'Phương Anh Đào, Tuấn Trần', N'Tiếng Việt', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE()),
(@M18, N'Gladiator II', N'gladiator-ii', N'Hậu duệ của Maximus bị đẩy vào đấu trường La Mã và buộc phải chiến đấu để giành lại danh dự cùng tương lai của đế chế.', @M18Dur, DATEADD(month,2,GETUTCDATE()), NULL, N'https://image.tmdb.org/t/p/original/2cxhvwyEwRlysAmRH4iodkvo0z5.jpg', N'https://www.youtube.com/watch?v=4jrE-Fu2XCA', N'Ridley Scott', N'Paul Mescal, Denzel Washington, Pedro Pascal', N'Tiếng Anh', N'Phụ đề Tiếng Việt', 1, @AgeT16, GETUTCDATE());

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

GO

-- ── Part D2: Generated movies (300), people (240) and Cast & Crew credits ──────
-- Deterministic, set-based: every index is derived from the tally number n via
-- modular/mixed-radix arithmetic (never RAND()), so row counts/distributions are
-- stable across runs while the GUIDs differ. NEWID() values that are referenced
-- more than once are always materialized into a #temp table first (never a CTE),
-- since a repeated reference to NEWID() inside a CTE re-evaluates it per reference.
DROP TABLE IF EXISTS #Person;
DROP TABLE IF EXISTS #MovieGen;
DROP TABLE IF EXISTS #CreditGen;

DECLARE @Today2 datetime = CAST(CAST(GETUTCDATE() AS date) AS datetime);

-- ── People: 240 total — VN pool Seq 0-99 (25 directors, 75 actors), EN pool Seq 100-239 (35 directors, 105 actors) ──
CREATE TABLE #Person (
    Seq  int NOT NULL,
    Pool nchar(2) NOT NULL,
    Role nchar(1) NOT NULL,
    Id   uniqueidentifier NOT NULL DEFAULT NEWID(),
    Name nvarchar(200) NOT NULL
);

INSERT INTO #Person (Seq, Pool, Role, Name)
SELECT seq.n, N'VN', CASE WHEN seq.n < 25 THEN N'D' ELSE N'A' END,
    fam.Word + N' ' + mid.Word + N' ' + giv.Word
FROM (SELECT TOP (100) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.all_objects) seq
JOIN (VALUES (0,N'Nguyễn'),(1,N'Trần'),(2,N'Lê'),(3,N'Phạm'),(4,N'Hoàng'),(5,N'Huỳnh'),(6,N'Phan'),(7,N'Vũ'),(8,N'Võ'),(9,N'Đặng'),(10,N'Bùi'),(11,N'Đỗ'),(12,N'Hồ'),(13,N'Ngô')) AS fam(Idx, Word) ON fam.Idx = seq.n % 14
JOIN (VALUES (0,N'Văn'),(1,N'Thị'),(2,N'Hữu'),(3,N'Đức'),(4,N'Minh'),(5,N'Ngọc'),(6,N'Thanh'),(7,N'Quang')) AS mid(Idx, Word) ON mid.Idx = (seq.n / 14) % 8
JOIN (VALUES (0,N'An'),(1,N'Bình'),(2,N'Cường'),(3,N'Dũng'),(4,N'Giang'),(5,N'Hải'),(6,N'Hùng'),(7,N'Khôi'),(8,N'Linh'),(9,N'Minh'),(10,N'Nam'),(11,N'Phong'),(12,N'Quân'),(13,N'Sơn'),(14,N'Tâm'),(15,N'Thảo'),(16,N'Trang'),(17,N'Tuấn'),(18,N'Vy'),(19,N'Yến')) AS giv(Idx, Word) ON giv.Idx = (seq.n / (14 * 8)) % 20;

INSERT INTO #Person (Seq, Pool, Role, Name)
SELECT 100 + seq.n, N'EN', CASE WHEN seq.n < 35 THEN N'D' ELSE N'A' END,
    fn.Word + N' ' + ln.Word
FROM (SELECT TOP (140) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.all_objects) seq
JOIN (VALUES (0,N'James'),(1,N'Michael'),(2,N'Robert'),(3,N'David'),(4,N'Daniel'),(5,N'Sarah'),(6,N'Emma'),(7,N'Olivia'),(8,N'Sophia'),(9,N'Grace'),(10,N'Ethan'),(11,N'Lucas'),(12,N'Henry'),(13,N'Nathan'),(14,N'Owen'),(15,N'Ava'),(16,N'Mia'),(17,N'Chloe'),(18,N'Ryan'),(19,N'Jack'),(20,N'Noah'),(21,N'Liam'),(22,N'Mason'),(23,N'Isabella'),(24,N'Amelia')) AS fn(Idx, Word) ON fn.Idx = seq.n % 25
JOIN (VALUES (0,N'Carter'),(1,N'Bennett'),(2,N'Harrison'),(3,N'Mitchell'),(4,N'Sullivan'),(5,N'Whitfield'),(6,N'Hayes'),(7,N'Donovan'),(8,N'Sinclair'),(9,N'Hartley'),(10,N'Pierce'),(11,N'Lawson'),(12,N'Monroe'),(13,N'Kingston'),(14,N'Fletcher'),(15,N'Griffin'),(16,N'Sheppard'),(17,N'Abernathy'),(18,N'Blackwood'),(19,N'Ashford'),(20,N'Crawford'),(21,N'Delgado'),(22,N'Ellison'),(23,N'Farrow'),(24,N'Graham')) AS ln(Idx, Word) ON ln.Idx = (seq.n / 25) % 25;

INSERT INTO [Person] ([Id],[Name],[PhotoUrl],[CreationTime])
SELECT Id, Name, NULL, GETUTCDATE() FROM #Person;

-- ── Movies: 300 generated (n=0..179 EN word-bank titles, n=180..299 VN word-bank titles) ──
CREATE TABLE #MovieGen (
    n                  int              NOT NULL PRIMARY KEY,
    Pool               nchar(2)         NOT NULL,
    Bucket             int              NOT NULL,
    Title              nvarchar(300)    NULL,
    SlugBase           nvarchar(320)    NULL,
    Slug               nvarchar(320)    NULL,
    PrimaryGenreIdx    int              NULL,
    SecondaryGenreIdx  int              NULL,
    Duration           int              NULL,
    ReleaseDate        date             NULL,
    EndDate            date             NULL,
    IsActive           bit              NULL,
    AgeCode            nvarchar(5)      NULL,
    Language           nvarchar(100)    NULL,
    Description        nvarchar(2000)   NULL,
    Id                 uniqueidentifier NOT NULL DEFAULT NEWID(),
    DirectorSeq        int              NULL
);

INSERT INTO #MovieGen (n, Pool, Bucket)
SELECT seq.n, CASE WHEN seq.n < 180 THEN N'EN' ELSE N'VN' END, seq.n % 20
FROM (SELECT TOP (300) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.all_objects) seq;

-- Titles/slugs — EN pool: "The {Adjective} {Noun}" (15 x 12 = 180 unique combinations)
UPDATE g
SET Title = N'The ' + adj.Word + N' ' + noun.Word,
    SlugBase = 'the-' + adj.Slug + '-' + noun.Slug
FROM #MovieGen g
JOIN (VALUES (0,N'Silent','silent'),(1,N'Broken','broken'),(2,N'Hidden','hidden'),(3,N'Last','last'),(4,N'Crimson','crimson'),(5,N'Frozen','frozen'),(6,N'Lost','lost'),(7,N'Golden','golden'),(8,N'Shattered','shattered'),(9,N'Eternal','eternal'),(10,N'Burning','burning'),(11,N'Forgotten','forgotten'),(12,N'Scarlet','scarlet'),(13,N'Wild','wild'),(14,N'Midnight','midnight')) AS adj(Idx, Word, Slug) ON adj.Idx = g.n % 15
JOIN (VALUES (0,N'Storm','storm'),(1,N'Kingdom','kingdom'),(2,N'Horizon','horizon'),(3,N'Legacy','legacy'),(4,N'Shadow','shadow'),(5,N'Empire','empire'),(6,N'Secret','secret'),(7,N'Journey','journey'),(8,N'Rebellion','rebellion'),(9,N'Prophecy','prophecy'),(10,N'Guardian','guardian'),(11,N'Labyrinth','labyrinth')) AS noun(Idx, Word, Slug) ON noun.Idx = g.n / 15
WHERE g.Pool = N'EN';

-- Titles/slugs — VN pool: "{Noun phrase} {Adjective phrase}" (12 x 10 = 120 unique combinations)
UPDATE g
SET Title = noun.Word + N' ' + adj.Word,
    SlugBase = noun.Slug + '-' + adj.Slug
FROM #MovieGen g
JOIN (VALUES (0,N'Lời Hứa','loi-hua'),(1,N'Cơn Mưa','con-mua'),(2,N'Giấc Mơ','giac-mo'),(3,N'Ánh Trăng','anh-trang'),(4,N'Dòng Sông','dong-song'),(5,N'Cánh Đồng','canh-dong'),(6,N'Ngọn Lửa','ngon-lua'),(7,N'Con Đường','con-duong'),(8,N'Bóng Tối','bong-toi'),(9,N'Ánh Sáng','anh-sang'),(10,N'Mảnh Ghép','manh-ghep'),(11,N'Hơi Thở','hoi-tho')) AS noun(Idx, Word, Slug) ON noun.Idx = (g.n - 180) % 12
JOIN (VALUES (0,N'Cuối Cùng','cuoi-cung'),(1,N'Không Tên','khong-ten'),(2,N'Giữa Đời','giua-doi'),(3,N'Trong Mơ','trong-mo'),(4,N'Chưa Kể','chua-ke'),(5,N'Bỏ Lại','bo-lai'),(6,N'Không Lời','khong-loi'),(7,N'Giữa Phố','giua-pho'),(8,N'Xa Xôi','xa-xoi'),(9,N'Không Ngủ','khong-ngu')) AS adj(Idx, Word, Slug) ON adj.Idx = (g.n - 180) / 12
WHERE g.Pool = N'VN';

-- Genres (genre index: 0 Hành Động, 1 Hài, 2 Chính Kịch, 3 Kinh Dị, 4 Khoa Học Viễn Tưởng, 5 Hoạt Hình, 6 Tình Cảm, 7 Giật Gân, 8 Phiêu Lưu, 9 Nhạc Kịch)
UPDATE g
SET PrimaryGenreIdx = g.n % 10,
    SecondaryGenreIdx = CASE WHEN g.n % 3 <> 0 AND (g.n * 3 + 1) % 10 <> g.n % 10 THEN (g.n * 3 + 1) % 10 ELSE NULL END
FROM #MovieGen g;

-- Duration: 85-165 minutes (Animation runs shorter, 85-105) — always under the 170min "fits a 195min slot" cap
UPDATE g
SET Duration = CASE WHEN g.PrimaryGenreIdx = 5 THEN 85 + g.n % 21 ELSE 85 + (g.n * 13) % 81 END
FROM #MovieGen g;

-- Release/End dates + IsActive, by 20-way bucket: 0-2 ended, 3 ended+inactive, 4-13 now showing, 14-19 upcoming
UPDATE g
SET ReleaseDate = CASE
        WHEN g.Bucket <= 3  THEN DATEADD(day, -(120 + (g.n * 37) % 240), CAST(@Today2 AS date))
        WHEN g.Bucket <= 13 THEN DATEADD(day, -(1 + (g.n * 37) % 90), CAST(@Today2 AS date))
        ELSE                     DATEADD(day, (3 + (g.n * 29) % 118), CAST(@Today2 AS date))
    END,
    EndDate = CASE
        WHEN g.Bucket <= 3  THEN DATEADD(day, -(1 + g.n % 60), CAST(@Today2 AS date))
        WHEN g.Bucket <= 13 THEN DATEADD(day, (14 + (g.n * 11) % 77), CAST(@Today2 AS date))
        ELSE NULL
    END,
    IsActive = CASE WHEN g.Bucket = 3 THEN 0 ELSE 1 END
FROM #MovieGen g;

-- Age restriction code
UPDATE g
SET AgeCode = CASE
        WHEN g.PrimaryGenreIdx = 3 THEN CASE WHEN g.n % 2 = 0 THEN N'T18' ELSE N'T16' END
        WHEN g.PrimaryGenreIdx = 5 THEN CASE WHEN g.n % 2 = 0 THEN N'P' ELSE N'K' END
        ELSE (SELECT v.Code FROM (VALUES (0,N'P'),(1,N'K'),(2,N'T13'),(3,N'T13'),(4,N'T16')) AS v(Idx,Code) WHERE v.Idx = g.n % 5)
    END
FROM #MovieGen g;

-- Language
UPDATE g
SET Language = CASE
        WHEN g.Pool = N'VN' THEN N'Tiếng Việt'
        WHEN g.n % 10 <= 6 THEN N'Tiếng Anh'
        WHEN g.n % 10 = 7 THEN N'Tiếng Hàn'
        WHEN g.n % 10 = 8 THEN N'Tiếng Nhật'
        ELSE N'Tiếng Pháp'
    END
FROM #MovieGen g;

-- Description (2 variants per genre)
UPDATE g
SET Description = d.Text
FROM #MovieGen g
JOIN (VALUES
    (0,0,N'Một chiến binh đơn độc phải đối đầu với thế lực hùng mạnh để bảo vệ những người mình yêu thương.'),
    (0,1,N'Cuộc chạm trán nảy lửa giữa hai phe đối lập đẩy cả thành phố vào vòng xoáy bạo lực không lối thoát.'),
    (1,0,N'Một nhóm bạn thân vướng vào loạt tình huống dở khóc dở cười trong chuyến đi định mệnh.'),
    (1,1,N'Sự nhầm lẫn tai hại biến một kế hoạch hoàn hảo thành chuỗi rắc rối hài hước.'),
    (2,0,N'Những rạn nứt trong một gia đình dần được hé lộ qua nhiều thế hệ đầy biến động.'),
    (2,1,N'Một con người bình thường phải đối diện với bi kịch để tìm lại ý nghĩa cuộc sống.'),
    (3,0,N'Một thế lực bí ẩn ám ảnh những người sống sót cuối cùng trong ngôi nhà bị nguyền rủa.'),
    (3,1,N'Bóng tối trỗi dậy từ một lời nguyền cổ xưa, săn đuổi từng nạn nhân không thương tiếc.'),
    (4,0,N'Nhân loại đứng trước bờ vực diệt vong khi một công nghệ vượt tầm kiểm soát xuất hiện.'),
    (4,1,N'Một phi hành đoàn mạo hiểm bước vào vùng không gian chưa từng được khám phá.'),
    (5,0,N'Một chú thú nhỏ bé học cách trưởng thành qua hành trình đầy màu sắc và tiếng cười.'),
    (5,1,N'Thế giới tưởng tượng rực rỡ mở ra bài học ấm áp về tình bạn và lòng dũng cảm.'),
    (6,0,N'Hai con người từ hai thế giới khác biệt dần tìm thấy nhau giữa bộn bề cuộc sống.'),
    (6,1,N'Một chuyện tình đầy thử thách buộc họ phải lựa chọn giữa đam mê và trách nhiệm.'),
    (7,0,N'Một bí mật động trời được hé lộ khiến mọi niềm tin trước đó sụp đổ hoàn toàn.'),
    (7,1,N'Kẻ săn đuổi và con mồi đổi vai trong trò chơi tâm lý nghẹt thở đến phút cuối.'),
    (8,0,N'Một hành trình xuyên qua vùng đất xa lạ thử thách lòng can đảm của những kẻ phiêu lưu.'),
    (8,1,N'Kho báu huyền thoại dẫn lối cho nhóm thám hiểm vào những hiểm nguy không lường trước.'),
    (9,0,N'Âm nhạc trở thành sợi dây kết nối những tâm hồn lạc lõng tìm về với chính mình.'),
    (9,1,N'Một buổi trình diễn định mệnh thay đổi hoàn toàn số phận của người nghệ sĩ trẻ.')
) AS d(GenreIdx, Variant, Text) ON d.GenreIdx = g.PrimaryGenreIdx AND d.Variant = g.n % 2;

-- Slug uniqueness guard: within #MovieGen the word-bank mapping is already collision-free,
-- this is a backstop against any accidental clash with the 18 hand-typed movie slugs.
UPDATE g
SET Slug = CASE WHEN rn.rn = 1 AND NOT EXISTS (SELECT 1 FROM [Movie] m WHERE m.Slug = g.SlugBase)
                THEN g.SlugBase
                ELSE g.SlugBase + '-' + CAST(g.n AS varchar(10))
           END
FROM #MovieGen g
JOIN (SELECT n, SlugBase, ROW_NUMBER() OVER (PARTITION BY SlugBase ORDER BY n) AS rn FROM #MovieGen) rn ON rn.n = g.n;

-- Director assignment: VN movies draw from VN directors (Seq 0-24), EN movies from EN directors (Seq 100-134)
UPDATE g
SET DirectorSeq = CASE WHEN g.Pool = N'VN' THEN g.n % 25 ELSE 100 + (g.n % 35) END
FROM #MovieGen g;

-- ── Credits: 1 director + 3-6 actors per movie, actor pool indices spaced by a stride (17) coprime
-- with the actor pool sizes (75 VN / 105 EN), which keeps the actors in any one movie distinct ──
CREATE TABLE #CreditGen (
    MovieN        int              NOT NULL,
    PersonSeq     int              NOT NULL,
    CreditType    int              NOT NULL,
    CharacterName nvarchar(200)    NULL,
    DisplayOrder  int              NOT NULL
);

INSERT INTO #CreditGen (MovieN, PersonSeq, CreditType, CharacterName, DisplayOrder)
SELECT g.n, g.DirectorSeq, 1, NULL, 0
FROM #MovieGen g;

INSERT INTO #CreditGen (MovieN, PersonSeq, CreditType, CharacterName, DisplayOrder)
SELECT g.n,
    CASE WHEN g.Pool = N'VN' THEN 25  + (g.n * 11 + j.j * 17) % 75
         ELSE                 135 + (g.n * 11 + j.j * 17) % 105 END,
    2, ch.Name, j.j
FROM #MovieGen g
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6)) AS j(j)
JOIN (VALUES (0,N'Alex'),(1,N'Jordan'),(2,N'Kai'),(3,N'Morgan'),(4,N'Riley'),(5,N'Sam'),(6,N'Quinn'),(7,N'Reyes'),(8,N'Vikram'),(9,N'Elena'),(10,N'Minh'),(11,N'Lan'),(12,N'Hùng'),(13,N'Thảo'),(14,N'Đức'),(15,N'Nhi'),(16,N'Khang'),(17,N'Diễm'),(18,N'Phúc'),(19,N'Yến'),(20,N'Captain Rhodes'),(21,N'Doctor Lane'),(22,N'Detective Cole'),(23,N'Agent Park'),(24,N'Professor Hale'),(25,N'Commander Reyes'),(26,N'Chị Hạnh'),(27,N'Anh Tùng'),(28,N'Bà Tư'),(29,N'Ông Năm'),(30,N'Cô Mai'),(31,N'Thầy Long'),(32,N'Bác Sáu'),(33,N'Chú Bảy'),(34,N'Em Bống'),(35,N'Sĩ Quan Duy'),(36,N'Giáo Sư Linh'),(37,N'Thám Tử Khánh'),(38,N'Nữ Hoàng Băng'),(39,N'Vua Bóng Tối')) AS ch(Idx, Name) ON ch.Idx = (g.n + j.j) % 40
WHERE j.j <= 3 + g.n % 4;

-- 1. Movie
INSERT INTO [Movie] (Id, Title, Slug, Description, Duration, ReleaseDate, EndDate, PosterUrl, TrailerUrl, Director, Cast, Language, Subtitle, IsActive, AgeRestrictionId, CreationTime)
SELECT
    g.Id, g.Title, g.Slug, g.Description, g.Duration, g.ReleaseDate, g.EndDate,
    N'https://picsum.photos/seed/' + g.Slug + N'/500/750', NULL,
    dp.Name, casts.CastNames,
    g.Language, N'Phụ đề Tiếng Việt', g.IsActive,
    (SELECT Id FROM [AgeRestriction] WHERE Code = g.AgeCode),
    DATEADD(minute, -g.n, GETUTCDATE())
FROM #MovieGen g
JOIN #Person dp ON dp.Seq = g.DirectorSeq
CROSS APPLY (
    SELECT STRING_AGG(p.Name, N', ') WITHIN GROUP (ORDER BY c.DisplayOrder) AS CastNames
    FROM #CreditGen c
    JOIN #Person p ON p.Seq = c.PersonSeq
    WHERE c.MovieN = g.n AND c.CreditType = 2
) AS casts;

-- 2. MovieTypeDetail (primary genre always; secondary genre when kept)
INSERT INTO [MovieTypeDetail] (MovieId, MovieTypeId)
SELECT g.Id, mt.Id
FROM #MovieGen g
JOIN (VALUES (0,N'Hành Động'),(1,N'Hài'),(2,N'Chính Kịch'),(3,N'Kinh Dị'),(4,N'Khoa Học Viễn Tưởng'),(5,N'Hoạt Hình'),(6,N'Tình Cảm'),(7,N'Giật Gân'),(8,N'Phiêu Lưu'),(9,N'Nhạc Kịch')) AS genre(Idx, Name) ON genre.Idx = g.PrimaryGenreIdx
JOIN [MovieType] mt ON mt.Name = genre.Name
UNION ALL
SELECT g.Id, mt2.Id
FROM #MovieGen g
JOIN (VALUES (0,N'Hành Động'),(1,N'Hài'),(2,N'Chính Kịch'),(3,N'Kinh Dị'),(4,N'Khoa Học Viễn Tưởng'),(5,N'Hoạt Hình'),(6,N'Tình Cảm'),(7,N'Giật Gân'),(8,N'Phiêu Lưu'),(9,N'Nhạc Kịch')) AS genre2(Idx, Name) ON genre2.Idx = g.SecondaryGenreIdx
JOIN [MovieType] mt2 ON mt2.Name = genre2.Name
WHERE g.SecondaryGenreIdx IS NOT NULL;

-- 3. MovieCredit
INSERT INTO [MovieCredit] (Id, MovieId, PersonId, CreditType, CharacterName, DisplayOrder, CreationTime)
SELECT NEWID(), g.Id, p.Id, c.CreditType, c.CharacterName, c.DisplayOrder, GETUTCDATE()
FROM #CreditGen c
JOIN #MovieGen g ON g.n = c.MovieN
JOIN #Person p ON p.Seq = c.PersonSeq;

DROP TABLE #CreditGen;
DROP TABLE #MovieGen;
DROP TABLE #Person;

GO

-- ── Part D3: ShowTime + ShowTimeRoom grid across every active movie and room ──────
-- 22 days (today -7..+14) x 5 daily slots x every Room, skipping ~1/7 of cells so
-- rooms aren't fully booked. Slots 1-4 only pick movies whose Duration+25min buffer
-- fits the 195min slot spacing; slot 5 (last of the day) has no such cap, so the
-- few long hand-typed movies (Oppenheimer, Avengers: Endgame) only ever land there —
-- this guarantees no same-room overlap without any special-casing.
DROP TABLE IF EXISTS #Days;
DROP TABLE IF EXISTS #Rooms;
DROP TABLE IF EXISTS #DayMovies;
DROP TABLE IF EXISTS #DaySummary;
DROP TABLE IF EXISTS #ShowGen;

DECLARE @Today3 datetime = CAST(CAST(GETUTCDATE() AS date) AS datetime);

CREATE TABLE #Days (d int NOT NULL, TheDate date NOT NULL, TheDateTime datetime NOT NULL);
INSERT INTO #Days (d, TheDate, TheDateTime)
SELECT d.d, CAST(DATEADD(day, d.d, @Today3) AS date), DATEADD(day, d.d, @Today3)
FROM (VALUES (-7),(-6),(-5),(-4),(-3),(-2),(-1),(0),(1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) AS d(d);

CREATE TABLE #Rooms (
    RoomSeq        int              NOT NULL,
    RoomId         uniqueidentifier NOT NULL,
    RoomTypeName   nvarchar(100)    NOT NULL,
    SupportsThreeD bit              NOT NULL,
    CityGroup      nvarchar(20)     NOT NULL
);

INSERT INTO #Rooms (RoomSeq, RoomId, RoomTypeName, SupportsThreeD, CityGroup)
SELECT ROW_NUMBER() OVER (ORDER BY t.Name, r.Name), r.Id, rt.Name, rt.SupportsThreeD,
    CASE WHEN t.City = N'TP. Hồ Chí Minh' THEN N'HCM' WHEN t.City = N'Hà Nội' THEN N'HN' ELSE N'Other' END
FROM [Room] r
JOIN [RoomType] rt ON rt.Id = r.RoomTypeId
JOIN [Theater] t ON t.Id = r.TheaterId;

-- Movies eligible on each day, flagged for 3D-eligible genres (Action/Sci-Fi/Animation/Adventure)
CREATE TABLE #DayMovies (
    d              int              NOT NULL,
    MovieId        uniqueidentifier NOT NULL,
    Duration       int              NOT NULL,
    ReleaseDate    date             NOT NULL,
    ThreeDEligible bit              NOT NULL,
    FitsRank       int              NULL,
    AllRank        int              NULL
);

INSERT INTO #DayMovies (d, MovieId, Duration, ReleaseDate, ThreeDEligible)
SELECT dd.d, m.Id, m.Duration, m.ReleaseDate,
    CASE WHEN EXISTS (
        SELECT 1 FROM [MovieTypeDetail] mtd JOIN [MovieType] mt ON mt.Id = mtd.MovieTypeId
        WHERE mtd.MovieId = m.Id AND mt.Name IN (N'Hành Động', N'Khoa Học Viễn Tưởng', N'Hoạt Hình', N'Phiêu Lưu')
    ) THEN 1 ELSE 0 END
FROM #Days dd
JOIN [Movie] m ON m.IsActive = 1 AND m.ReleaseDate <= dd.TheDate AND (m.EndDate IS NULL OR m.EndDate >= dd.TheDate);

UPDATE dm
SET FitsRank = x.FitsRank
FROM #DayMovies dm
JOIN (
    SELECT d, MovieId, ROW_NUMBER() OVER (PARTITION BY d ORDER BY MovieId) AS FitsRank
    FROM #DayMovies
    WHERE Duration + 25 <= 195
) x ON x.d = dm.d AND x.MovieId = dm.MovieId;

UPDATE dm
SET AllRank = x.AllRank
FROM #DayMovies dm
JOIN (
    SELECT d, MovieId, ROW_NUMBER() OVER (PARTITION BY d ORDER BY MovieId) AS AllRank
    FROM #DayMovies
) x ON x.d = dm.d AND x.MovieId = dm.MovieId;

CREATE TABLE #DaySummary (d int NOT NULL PRIMARY KEY, FitsCount int NOT NULL, AllCount int NOT NULL);
INSERT INTO #DaySummary (d, FitsCount, AllCount)
SELECT d, SUM(CASE WHEN Duration + 25 <= 195 THEN 1 ELSE 0 END), COUNT(*)
FROM #DayMovies
GROUP BY d;

CREATE TABLE #ShowGen (
    Id             uniqueidentifier NOT NULL DEFAULT NEWID(),
    MovieId        uniqueidentifier NOT NULL,
    RoomId         uniqueidentifier NOT NULL,
    StartTime      datetime         NOT NULL,
    EndTime        datetime         NOT NULL,
    ProjectionForm int              NOT NULL,
    ShowTimeType   int              NOT NULL,
    BasePrice      float            NOT NULL
);

INSERT INTO #ShowGen (MovieId, RoomId, StartTime, EndTime, ProjectionForm, ShowTimeType, BasePrice)
SELECT
    picked.MovieId,
    r.RoomId,
    DATEADD(minute, slot.StartMinute, dd.TheDateTime),
    DATEADD(minute, picked.Duration, DATEADD(minute, slot.StartMinute, dd.TheDateTime)),
    CASE WHEN r.SupportsThreeD = 1 AND picked.ThreeDEligible = 1 THEN 2 ELSE 1 END,
    CASE
        WHEN dd.TheDate = picked.ReleaseDate AND slot.SlotNo = 4 THEN 1
        WHEN r.RoomTypeName = N'IMAX' AND DATEPART(WEEKDAY, dd.TheDate) = 7 AND slot.SlotNo = 5 THEN 2
        ELSE 0
    END,
    price.Base + CASE r.CityGroup WHEN N'HCM' THEN 0 WHEN N'HN' THEN -5000 ELSE -10000 END
FROM #Rooms r
CROSS JOIN #Days dd
JOIN #DaySummary ds ON ds.d = dd.d AND ds.AllCount > 0
CROSS JOIN (VALUES (1,540),(2,735),(3,930),(4,1125),(5,1320)) AS slot(SlotNo, StartMinute)
JOIN (VALUES (N'2D',85000),(N'3D',95000),(N'Deluxe',125000),(N'IMAX',130000),(N'4DX',135000)) AS price(TypeName, Base) ON price.TypeName = r.RoomTypeName
CROSS APPLY (
    SELECT TOP 1 dm.MovieId, dm.Duration, dm.ReleaseDate, dm.ThreeDEligible
    FROM #DayMovies dm
    WHERE dm.d = dd.d
      AND (
            (slot.SlotNo <= 4 AND ds.FitsCount > 0 AND dm.FitsRank = ((r.RoomSeq * 7 + dd.d * 3 + slot.SlotNo) % NULLIF(ds.FitsCount, 0)) + 1)
         OR ((slot.SlotNo = 5 OR ds.FitsCount = 0) AND dm.AllRank = ((r.RoomSeq * 7 + dd.d * 3 + slot.SlotNo) % NULLIF(ds.AllCount, 0)) + 1)
      )
) AS picked
WHERE (r.RoomSeq + dd.d * 3 + slot.SlotNo) % 7 <> 0;

INSERT INTO [ShowTime] (Id, MovieId, StartTime, EndTime, ProjectionForm, ShowTimeType, IsActive, CreationTime)
SELECT Id, MovieId, StartTime, EndTime, ProjectionForm, ShowTimeType, 1, GETUTCDATE()
FROM #ShowGen;

INSERT INTO [ShowTimeRoom] (ShowTimeId, RoomId, BasePrice)
SELECT Id, RoomId, BasePrice
FROM #ShowGen;

DROP TABLE #ShowGen;
DROP TABLE #DaySummary;
DROP TABLE #DayMovies;
DROP TABLE #Rooms;
DROP TABLE #Days;

GO

-- ── Part D4: Additional promotions (codes, auto-apply rules, time windows, edge cases) ──
INSERT INTO [Discount]
    ([Id], [Code], [Description], [Percent], [MaxDiscountAmount], [DiscountTypeId],
     [StartDate], [EndDate], [MaxUsage], [UsedCount], [IsActive],
     [AutoApply], [ApplyToAllTheaters], [MovieId], [DaysOfWeekMask], [StartTimeOfDay], [EndTimeOfDay],
     [CreationTime])
VALUES
-- Code promos
(NEWID(), N'NEWCUSTOMER5', N'Giảm 5% cho khách hàng mới', 5, 15000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 1000, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'FAMILY20', N'Giảm 20% cho nhóm từ 4 vé trở lên', 20, 50000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(month,6,GETUTCDATE()), 300, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'BIRTHDAY25', N'Giảm 25% dịp sinh nhật thành viên', 25, 60000, (SELECT Id FROM [DiscountType] WHERE Name = N'Thành Viên'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 150, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'VIP10', N'Giảm 10% cho hội viên VIP', 10, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Thành Viên'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 500, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'FLASH50', N'Flash sale giảm 50% số lượng có hạn', 50, 80000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(day,14,GETUTCDATE()), 50, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'REFER15', N'Giảm 15% khi giới thiệu bạn bè', 15, 35000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 400, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Auto-apply by day of week (DaysOfWeekMask bit0=Sunday..bit6=Saturday)
(NEWID(), NULL, N'Ưu đãi cuối tuần - tự động giảm 15%', 15, 40000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 65, NULL, NULL, GETUTCDATE()),
(NEWID(), NULL, N'Ưu đãi Thứ Hai đầu tuần - tự động giảm 12%', 12, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 2, NULL, NULL, GETUTCDATE()),
(NEWID(), NULL, N'Ưu đãi Thứ Tư vui vẻ - tự động giảm 10%', 10, 25000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 8, NULL, NULL, GETUTCDATE()),
(NEWID(), NULL, N'Ưu đãi Thứ Sáu cuối tuần - tự động giảm 18%', 18, 45000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 32, NULL, NULL, GETUTCDATE()),
-- Time-of-day windows
(NEWID(), NULL, N'Suất chiếu sớm (8h-12h) - tự động giảm 10%', 10, 20000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 127, '08:00', '12:00', GETUTCDATE()),
(NEWID(), NULL, N'Suất chiếu buổi trưa các ngày trong tuần - tự động giảm 12%', 12, 25000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 62, '12:00', '17:00', GETUTCDATE()),
(NEWID(), NULL, N'Suất chiếu khuya (22h-23h59) - tự động giảm 20%', 20, 40000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), NULL, 0, 1, 1, 1, NULL, 127, '22:00', '23:59', GETUTCDATE()),
-- Expired
(NEWID(), N'OLDPROMO1', N'Khuyến mãi đã kết thúc (demo dữ liệu quá hạn)', 10, 20000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), DATEADD(month,-6,GETUTCDATE()), DATEADD(month,-2,GETUTCDATE()), 200, 180, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'OLDPROMO2', N'Khuyến mãi theo mùa đã kết thúc (demo dữ liệu quá hạn)', 15, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), DATEADD(month,-8,GETUTCDATE()), DATEADD(month,-4,GETUTCDATE()), 150, 120, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Not yet started
(NEWID(), N'COMINGSOON1', N'Khuyến mãi sắp diễn ra', 15, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), DATEADD(day,30,GETUTCDATE()), DATEADD(day,120,GETUTCDATE()), 300, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'COMINGSOON2', N'Khuyến mãi theo mùa sắp diễn ra', 20, 40000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), DATEADD(day,30,GETUTCDATE()), DATEADD(day,150,GETUTCDATE()), 200, 0, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Inactive
(NEWID(), N'INACTIVE1', N'Mã khuyến mãi đã bị vô hiệu hóa', 10, 20000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 100, 0, 0, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Used up
(NEWID(), N'MAXEDOUT', N'Mã khuyến mãi đã hết lượt sử dụng', 15, 25000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 100, 100, 1, 0, 1, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Theater-specific
(NEWID(), N'FLAGSHIP10', N'Giảm 10% tại các cụm rạp hàng đầu', 10, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 500, 0, 1, 0, 0, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'SAIGON15', N'Giảm 15% tại các rạp khu vực TP. Hồ Chí Minh', 15, 35000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 500, 0, 1, 0, 0, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'HANOI12', N'Giảm 12% tại các rạp khu vực Hà Nội', 12, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 500, 0, 1, 0, 0, NULL, NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'COASTAL20', N'Giảm 20% tại các rạp ven biển và cao nguyên', 20, 40000, (SELECT Id FROM [DiscountType] WHERE Name = N'Theo Mùa'), GETUTCDATE(), DATEADD(year,1,GETUTCDATE()), 300, 0, 1, 0, 0, NULL, NULL, NULL, NULL, GETUTCDATE()),
-- Movie-specific
(NEWID(), N'WICKEDFAN', N'Giảm 20% khi xem Wicked', 20, 50000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(month,6,GETUTCDATE()), 300, 0, 1, 0, 1, (SELECT Id FROM [Movie] WHERE Slug = N'wicked'), NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'BOGIAFAMILY', N'Giảm 15% khi xem Bố Già cùng gia đình', 15, 30000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(month,6,GETUTCDATE()), 300, 0, 1, 0, 1, (SELECT Id FROM [Movie] WHERE Slug = N'bo-gia'), NULL, NULL, NULL, GETUTCDATE()),
(NEWID(), N'DEADPOOLVIP', N'Giảm 25% khi xem Deadpool & Wolverine', 25, 50000, (SELECT Id FROM [DiscountType] WHERE Name = N'Khuyến Mãi'), GETUTCDATE(), DATEADD(month,6,GETUTCDATE()), 200, 0, 1, 0, 1, (SELECT Id FROM [Movie] WHERE Slug = N'deadpool-wolverine'), NULL, NULL, NULL, GETUTCDATE());

INSERT INTO [DiscountTheater] ([Id], [DiscountId], [TheaterId], [CreationTime])
SELECT NEWID(), d.Id, t.Id, GETUTCDATE()
FROM (VALUES
    (N'FLAGSHIP10', N'Cinema Vạn Hạnh Mall'),
    (N'FLAGSHIP10', N'Cinema Times City'),
    (N'SAIGON15',   N'Cinema Crescent Mall'),
    (N'SAIGON15',   N'Cinema Aeon Tân Phú'),
    (N'SAIGON15',   N'Cinema Gigamall Thủ Đức'),
    (N'HANOI12',    N'Cinema Aeon Long Biên'),
    (N'HANOI12',    N'Cinema Lotte Đống Đa'),
    (N'COASTAL20',  N'Cinema Lotte Vũng Tàu'),
    (N'COASTAL20',  N'Cinema Vincom Nha Trang'),
    (N'COASTAL20',  N'Cinema Vincom Đà Lạt')
) AS r(Code, TheaterName)
JOIN [Discount] d ON d.Code = r.Code
JOIN [Theater] t ON t.Name = r.TheaterName;

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
