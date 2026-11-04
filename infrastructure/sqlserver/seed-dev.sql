-- Dữ liệu danh mục và bảng giá GIẢ LẬP cho môi trường dev. Không phải mức giá thật của bệnh viện
-- (mức giá cụ thể còn mở theo Phạm vi nghiệp vụ v2.5 §7). Không chạy trên Staging/Prod.
-- Chạy lại nhiều lần được: chỉ thêm dòng còn thiếu, tra theo mã (RankCode, RoomNumber, ServiceCode, PriceListCode).
-- Chạy sau khi đã áp đủ migration. Ví dụ trên SQL Server local:
--   sqlcmd -S localhost -E -C -b -f 65001 -d SmartHealthcare -i infrastructure/sqlserver/seed-dev.sql
-- Bắt buộc -f 65001: file là UTF-8, thiếu tùy chọn này sqlcmd đọc theo ANSI và làm hỏng tên tiếng Việt.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() = N'SmartHealthcare_Test'
    THROW 50001, N'Không seed vào SmartHealthcare_Test: DB test phải rỗng, mỗi test tự tạo dữ liệu rồi rollback.', 1;

BEGIN TRANSACTION;

-- identity.DoctorRank (RankCode theo ERD v2.4)
INSERT INTO [identity].DoctorRank (Id, RankCode, RankName, DisplayOrder, IsActive)
SELECT v.Id, v.RankCode, v.RankName, v.DisplayOrder, 1
FROM (VALUES
    (CAST('0a1f0001-0000-4000-8000-000000000001' AS uniqueidentifier), N'SpecialistI',   N'Bác sĩ chuyên khoa I',  1),
    (CAST('0a1f0001-0000-4000-8000-000000000002' AS uniqueidentifier), N'SpecialistII',  N'Bác sĩ chuyên khoa II', 2),
    (CAST('0a1f0001-0000-4000-8000-000000000003' AS uniqueidentifier), N'Master',        N'Thạc sĩ, Bác sĩ',       3),
    (CAST('0a1f0001-0000-4000-8000-000000000004' AS uniqueidentifier), N'Doctor',        N'Tiến sĩ, Bác sĩ',       4),
    (CAST('0a1f0001-0000-4000-8000-000000000005' AS uniqueidentifier), N'AssociateProf', N'Phó giáo sư',           5),
    (CAST('0a1f0001-0000-4000-8000-000000000006' AS uniqueidentifier), N'Professor',     N'Giáo sư',               6)
) AS v (Id, RankCode, RankName, DisplayOrder)
WHERE NOT EXISTS (SELECT 1 FROM [identity].DoctorRank r WHERE r.RankCode = v.RankCode);

-- identity.Room: phòng CLS làm phòng mặc định của dịch vụ
INSERT INTO [identity].Room (Id, RoomNumber, RoomName, RoomType, DepartmentName, IsActive)
SELECT v.Id, v.RoomNumber, v.RoomName, v.RoomType, v.DepartmentName, 1
FROM (VALUES
    (CAST('0a1f0002-0000-4000-8000-000000000001' AS uniqueidentifier), N'DEV-XN-01', N'Phòng xét nghiệm 1', N'Laboratory', N'Khoa Xét nghiệm'),
    (CAST('0a1f0002-0000-4000-8000-000000000002' AS uniqueidentifier), N'DEV-XQ-01', N'Phòng X-quang 1',    N'XRay',       N'Khoa Chẩn đoán hình ảnh'),
    (CAST('0a1f0002-0000-4000-8000-000000000003' AS uniqueidentifier), N'DEV-SA-01', N'Phòng siêu âm 1',    N'Ultrasound', N'Khoa Chẩn đoán hình ảnh'),
    (CAST('0a1f0002-0000-4000-8000-000000000004' AS uniqueidentifier), N'DEV-NS-01', N'Phòng nội soi 1',    N'Endoscopy',  N'Khoa Nội soi')
) AS v (Id, RoomNumber, RoomName, RoomType, DepartmentName)
WHERE NOT EXISTS (SELECT 1 FROM [identity].Room r WHERE r.RoomNumber = v.RoomNumber);

-- scheduling.MedicalService (DefaultRoomId là ID chéo module, tra theo RoomNumber)
INSERT INTO scheduling.MedicalService (Id, ServiceCode, ServiceName, ServiceCategory, DefaultRoomId, IsMandatoryDefault, IsActive)
SELECT v.Id, v.ServiceCode, v.ServiceName, v.ServiceCategory, room.Id, v.IsMandatoryDefault, 1
FROM (VALUES
    (CAST('0a1f0003-0000-4000-8000-000000000001' AS uniqueidentifier), N'DEV-XN-CTM',  N'Tổng phân tích tế bào máu',  N'Laboratory', N'DEV-XN-01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000002' AS uniqueidentifier), N'DEV-XN-SH',   N'Sinh hóa máu cơ bản',        N'Laboratory', N'DEV-XN-01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000003' AS uniqueidentifier), N'DEV-XQ-NGUC', N'Chụp X-quang ngực thẳng',    N'Radiology',  N'DEV-XQ-01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000004' AS uniqueidentifier), N'DEV-SA-BUNG', N'Siêu âm ổ bụng tổng quát',   N'Ultrasound', N'DEV-SA-01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000005' AS uniqueidentifier), N'DEV-NS-DD',   N'Nội soi dạ dày',             N'Endoscopy',  N'DEV-NS-01', CAST(0 AS bit))
) AS v (Id, ServiceCode, ServiceName, ServiceCategory, RoomNumber, IsMandatoryDefault)
JOIN [identity].Room room ON room.RoomNumber = v.RoomNumber
WHERE NOT EXISTS (SELECT 1 FROM scheduling.MedicalService s WHERE s.ServiceCode = v.ServiceCode);

-- billing.PriceList: một bảng giá dev hiệu lực từ 01/01/2026 00:00 giờ VN, không hết hạn
INSERT INTO billing.PriceList (Id, PriceListCode, PriceListName, Currency, EffectiveFromUtc, EffectiveToUtc, IsActive)
SELECT CAST('0a1f0004-0000-4000-8000-000000000001' AS uniqueidentifier), N'DEV-2026', N'Bảng giá giả lập dev 2026', N'VND',
       CAST('2025-12-31T17:00:00' AS datetime2), NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM billing.PriceList WHERE PriceListCode = N'DEV-2026');

DECLARE @priceListId uniqueidentifier = (SELECT Id FROM billing.PriceList WHERE PriceListCode = N'DEV-2026');

-- Phí khám theo cấp bậc (giả lập)
INSERT INTO billing.PriceListItem (Id, PriceListId, ItemType, DoctorRankId, DoctorId, MedicalServiceId, UnitPrice, IsActive)
SELECT NEWID(), @priceListId, N'ConsultationFee', rank.Id, NULL, NULL, v.UnitPrice, 1
FROM (VALUES
    (N'SpecialistI',   CAST(150000 AS decimal(18,2))),
    (N'SpecialistII',  CAST(200000 AS decimal(18,2))),
    (N'Master',        CAST(180000 AS decimal(18,2))),
    (N'Doctor',        CAST(250000 AS decimal(18,2))),
    (N'AssociateProf', CAST(350000 AS decimal(18,2))),
    (N'Professor',     CAST(500000 AS decimal(18,2)))
) AS v (RankCode, UnitPrice)
JOIN [identity].DoctorRank rank ON rank.RankCode = v.RankCode
WHERE NOT EXISTS (
    SELECT 1 FROM billing.PriceListItem i
    WHERE i.PriceListId = @priceListId AND i.ItemType = N'ConsultationFee' AND i.DoctorRankId = rank.Id AND i.DoctorId IS NULL);

-- Phí dịch vụ CLS (giả lập)
INSERT INTO billing.PriceListItem (Id, PriceListId, ItemType, DoctorRankId, DoctorId, MedicalServiceId, UnitPrice, IsActive)
SELECT NEWID(), @priceListId, N'CLSService', NULL, NULL, svc.Id, v.UnitPrice, 1
FROM (VALUES
    (N'DEV-XN-CTM',  CAST(90000 AS decimal(18,2))),
    (N'DEV-XN-SH',   CAST(150000 AS decimal(18,2))),
    (N'DEV-XQ-NGUC', CAST(120000 AS decimal(18,2))),
    (N'DEV-SA-BUNG', CAST(200000 AS decimal(18,2))),
    (N'DEV-NS-DD',   CAST(800000 AS decimal(18,2)))
) AS v (ServiceCode, UnitPrice)
JOIN scheduling.MedicalService svc ON svc.ServiceCode = v.ServiceCode
WHERE NOT EXISTS (
    SELECT 1 FROM billing.PriceListItem i
    WHERE i.PriceListId = @priceListId AND i.ItemType = N'CLSService' AND i.MedicalServiceId = svc.Id);

COMMIT TRANSACTION;

SELECT
    (SELECT COUNT(*) FROM [identity].DoctorRank) AS DoctorRanks,
    (SELECT COUNT(*) FROM scheduling.MedicalService) AS MedicalServices,
    (SELECT COUNT(*) FROM billing.PriceListItem WHERE PriceListId = @priceListId) AS PriceItemsInDev2026;
