-- Dịch vụ CLS và bảng giá GIẢ LẬP cho môi trường dev. Không phải mức giá thật của bệnh viện. Không chạy trên Staging/Prod.
-- Cấp bậc bác sĩ, phòng và tài khoản demo do IdentityPatientsSeedService seed khi Api chạy ở Development;
-- script này không tạo lại chúng, chỉ tra theo mã (RankCode, RoomNumber), nên không lệch Id với seed của Identity.
-- Chạy lại nhiều lần được: chỉ thêm dòng còn thiếu, tra theo ServiceCode, PriceListCode.
-- Chạy sau khi đã áp đủ migration và Api đã khởi động một lần. Ví dụ trên SQL Server local:
--   sqlcmd -S localhost -E -C -b -f 65001 -d SmartHealthcare -i infrastructure/sqlserver/seed-dev.sql
-- Bắt buộc -f 65001: file là UTF-8, thiếu tùy chọn này sqlcmd đọc theo ANSI và làm hỏng tên tiếng Việt.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() = N'SmartHealthcare_Test'
    THROW 50001, N'Không seed vào SmartHealthcare_Test: DB test phải rỗng, mỗi test tự tạo dữ liệu rồi rollback.', 1;

-- Chờ seed của Identity (compose chạy song song với Api): đủ 6 cấp bậc và 4 phòng CLS, tối đa 5 phút.
DECLARE @tries int = 0;
WHILE ((SELECT COUNT(*) FROM [identity].DoctorRank
        WHERE RankCode IN (N'SpecialistI', N'SpecialistII', N'Master', N'Doctor', N'AssociateProf', N'Professor')) < 6
    OR (SELECT COUNT(*) FROM [identity].Room WHERE RoomNumber IN (N'XN01', N'XQ01', N'SA01', N'NS01')) < 4)
BEGIN
    IF @tries >= 60
        THROW 50002, N'Chưa có cấp bậc hoặc phòng CLS do Api seed. Chạy Api ở Development (cần Seed:DefaultPassword) rồi chạy lại script.', 1;
    SET @tries += 1;
    WAITFOR DELAY '00:00:05';
END;

BEGIN TRANSACTION;

-- scheduling.MedicalService (DefaultRoomId là ID chéo module, tra theo RoomNumber của Identity)
INSERT INTO scheduling.MedicalService (Id, ServiceCode, ServiceName, ServiceCategory, DefaultRoomId, IsMandatoryDefault, IsActive)
SELECT v.Id, v.ServiceCode, v.ServiceName, v.ServiceCategory, room.Id, v.IsMandatoryDefault, 1
FROM (VALUES
    (CAST('0a1f0003-0000-4000-8000-000000000001' AS uniqueidentifier), N'DEV-XN-CTM',  N'Tổng phân tích tế bào máu',  N'Laboratory', N'XN01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000002' AS uniqueidentifier), N'DEV-XN-SH',   N'Sinh hóa máu cơ bản',        N'Laboratory', N'XN01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000003' AS uniqueidentifier), N'DEV-XQ-NGUC', N'Chụp X-quang ngực thẳng',    N'Radiology',  N'XQ01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000004' AS uniqueidentifier), N'DEV-SA-BUNG', N'Siêu âm ổ bụng tổng quát',   N'Ultrasound', N'SA01', CAST(1 AS bit)),
    (CAST('0a1f0003-0000-4000-8000-000000000005' AS uniqueidentifier), N'DEV-NS-DD',   N'Nội soi dạ dày',             N'Endoscopy',  N'NS01', CAST(0 AS bit))
) AS v (Id, ServiceCode, ServiceName, ServiceCategory, RoomNumber, IsMandatoryDefault)
JOIN [identity].Room room ON room.RoomNumber = v.RoomNumber
WHERE NOT EXISTS (SELECT 1 FROM scheduling.MedicalService s WHERE s.ServiceCode = v.ServiceCode);

-- billing.PriceList: một bảng giá dev hiệu lực từ 01/01/2026 00:00 giờ VN, không hết hạn
INSERT INTO billing.PriceList (Id, PriceListCode, PriceListName, Currency, EffectiveFromUtc, EffectiveToUtc, IsActive)
SELECT CAST('0a1f0004-0000-4000-8000-000000000001' AS uniqueidentifier), N'DEV-2026', N'Bảng giá giả lập dev 2026', N'VND',
       CAST('2025-12-31T17:00:00' AS datetime2), NULL, 1
WHERE NOT EXISTS (SELECT 1 FROM billing.PriceList WHERE PriceListCode = N'DEV-2026');

DECLARE @priceListId uniqueidentifier = (SELECT Id FROM billing.PriceList WHERE PriceListCode = N'DEV-2026');

-- Phí khám theo cấp bậc (giả lập); bác sĩ demo của Identity đều có cấp bậc nên đều báo giá được.
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
