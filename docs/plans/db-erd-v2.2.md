# Kế hoạch dựng database từ ERD v2.2 (CLA-DB-001)

Trạng thái: **chờ trưởng nhóm duyệt**. Chưa viết code.

## 1. Kết quả bước 0

### 1.1 File ERD
- Đã lưu nguyên văn tại `docs/db/erd-v2.2.json` (SHA-256 `2289aaffa7e193358dc632441db7e8601f6be420631681b53ede808e1a807b57`, `cmp` giống bản gốc trong Downloads).
- 46 bảng (identity 9, catalog 7, scheduling 8, clinical 10, billing 7, workflow 5), 78 quan hệ, `pending_decisions` rỗng, `decided` 4 mục (1B, 2A, 3B, 4B), `fk_without_drawn_line` 19 mục.
- Phân tích bằng script: 98 cột FK (46 nội bộ module, 52 xuyên module, không cột nào không xác định được bảng đích), 52 cột enum, 35 UK đơn cột, 29 cột ghi "Nullable".

### 1.2 Tài liệu tham chiếu
| Tài liệu | Đường dẫn |
|---|---|
| Ranh giới module (MOD-01..05) | `docs/ai/instructions/database-module-boundaries.md` |
| Quy ước kiểu DB-05 | `docs/ai/instructions/sqlserver-efcore.md` |
| Quy trình migration DB-03 | `docs/ai/instructions/sqlserver-efcore.md` |
| Nguồn của MOD-01 | `docs/architecture/giai-phap-ky-thuat-v2.2.md` §8.1, §8.2 |

### 1.3 Công cụ
| Công cụ | Có | Ghi chú |
|---|---|---|
| Graphify | Không | Bỏ bước. Backend chỉ có thư mục rỗng (`.gitkeep`), không có `.sln`, `.csproj`, DbContext hay entity nào. |
| Superpowers | Có | brainstorming, writing-plans, test-driven-development, executing-plans, systematic-debugging, verification-before-completion, requesting-code-review |
| Context7 | Có | skill `find-docs` / `npx ctx7` |
| Ponytail | Có | |
| RTK | Có | |
| Security Guidance / security-and-hardening | Có | plugin security-guidance và skill security-and-hardening |
| Gitleaks, Semgrep | Có | |
| Archify | Có | |
| .NET SDK | 10.0.401 | |
| dotnet-ef | Không | Cài dạng local tool (`.config/dotnet-tools.json`) |
| Docker | 29.7.2 | Dùng chạy SQL Server cho test |

`/plugin` là hộp thoại tương tác, không chạy được trong phiên này; danh sách lấy từ `~/.claude/plugins/cache`, `~/.claude/skills/`, `.claude/skills/`.

## 2. Mâu thuẫn với tài liệu và cách xử lý

Trưởng nhóm chọn **theo ERD** (6 schema). Các điểm lệch sẽ ghi vào báo cáo cuối và handoff cho Codex để cập nhật Giải pháp:

| Điểm | ERD (áp dụng) | Giải pháp v2.2 / repo |
|---|---|---|
| PriceList, PriceListItem | schema `catalog` | §8.1.5: Billing sở hữu bảng giá |
| MedicationCatalog | schema `catalog` | §8.1.3: module MedicationCatalog riêng |
| Patient, PatientRepresentative | schema `identity`, module Identity | repo có thư mục `Patients` riêng; để nguyên, không dùng |
| BookingAttempt, Operation, ApiIdempotencyRecord, OutboxMessage, AuditLog | schema `workflow`, thư mục mới `Workflow` | §8.1.3: Workflow "không sở hữu dữ liệu nghiệp vụ"; §8.2.2 đặt Outbox ở Shared.Infrastructure |
| Catalog | thư mục mới `Catalog` | không có trong §8.1 |
| Operation.Status | theo ERD (có `Failed`, không có `RetryScheduled`) | v2.2 ghi là "tên gợi ý" |
| Tên bảng quota | `DoctorSessionSlotQuota` | v2.2 gọi `DoctorDayQuota` |

Thư mục `Documents`, `Notifications`, `Patients`, `Reporting` giữ nguyên, không đụng.

## 3. Cấu trúc code

Theo khung thư mục đã có (`backend/src/Modules/<Module>/<Layer>/`), mỗi module một project Domain (entity, enum) và một project Infrastructure (DbContext, `IEntityTypeConfiguration<T>`, migration):

```text
backend/
  SmartHealthcare.slnx
  src/
    Modules/<Module>/Domain/SmartHealthcare.<Module>.Domain.csproj
    Modules/<Module>/Infrastructure/SmartHealthcare.<Module>.Infrastructure.csproj
    Shared/Persistence/SmartHealthcare.Shared.Persistence.csproj   (UTC converter, helper CHECK enum)
    Tools/DbMigrator/SmartHealthcare.DbMigrator.csproj               (tác vụ migrate riêng)
tests/backend/Database.Tests/                                         (xUnit)
Directory.Packages.props                                             (ghim phiên bản package)
.config/dotnet-tools.json                                            (dotnet-ef)
```

- `<Module>` ∈ Identity, Catalog, Scheduling, Clinical, Billing, Workflow; DbContext tương ứng `IdentityDbContext` … `WorkflowDbContext`, mỗi cái `HasDefaultSchema("<domain>")` và bảng lịch sử migration `__EFMigrationsHistory` trong schema của mình.
- Không generic repository, không base entity, không repository/service/API, không seed (theo giới hạn của prompt).
- `Shared.Persistence` là shared kernel tối thiểu theo MOD-04: chỉ có converter UTC và một hàm dựng CHECK cho enum, để 52 CHECK không phải viết tay.
- Package (ghim trong `Directory.Packages.props`): `Microsoft.EntityFrameworkCore.SqlServer` 10.x, `Microsoft.EntityFrameworkCore.Design` 10.x, `Microsoft.Data.SqlClient` (kéo theo), `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. Không thêm Testcontainers: test dùng SQL Server trong Docker, connection string qua biến môi trường.

Thứ tự module: identity → catalog → scheduling → clinical → billing → workflow; mỗi module: test đỏ → entity + cấu hình → test xanh → migration → script SQL → review → commit.

## 4. Quy tắc map kiểu (DB-05)

| ERD | EF / SQL Server |
|---|---|
| guid | `uniqueidentifier`; PK sinh phía ứng dụng bằng sequential GUID mặc định của EF SQL Server (không IDENTITY) |
| decimal tiền | `decimal(18,2)`: AppliedConsultationFee, PriceListItem.UnitPrice, BillingItem.UnitPrice/TotalAmount/PaidAmount/RefundedAmount, PaymentIntent.TotalExpectedAmount, PaymentTransaction.TransactionAmount, PaymentAllocation.AllocatedAmount, RefundRequest.RequestedAmount, RefundTransaction.RefundedAmount, ServiceOrderItem.AppliedPrice |
| decimal sinh hiệu | `decimal(5,2)`: WeightKg, HeightCm, SystolicBP, DiastolicBP, BodyTemperature, SpO2 |
| datetime | `datetime2`, converter ép `DateTimeKind.Utc` khi đọc; không đặt default giờ ICT |
| date | `date` (`DateOnly`) |
| bigint/int/bool | `bigint`/`int`/`bit` |
| rowversion | `rowversion`, `IsRowVersion()` |
| enum | `nvarchar(32)` qua `HasConversion<string>()` + CHECK `IN (...)` (52 cột) |

MaxLength đề xuất cho cột string ERD không ghi độ dài:

| Nhóm | Độ dài | Cột |
|---|---|---|
| Mã, số | 50 | *Code, RoomNumber, AppointmentNumber, IntentNumber, TicketNumber, AccountCode, RankCode, ServiceCode, MedicationCode, PriceListCode, PrescriptionCode, StationCode |
| Tên | 200 | FullName, *Name, *NameSnapshot, *Snapshot, DepartmentName, ActiveIngredient(Snapshot), Strength, Dosage, Frequency |
| Đăng nhập | 256 | UserName, Email |
| Điện thoại | 20 | PhoneNumber |
| CCCD | 20 | NationalId |
| Mã ICD-10 | 10 | ICD10Code |
| Địa chỉ, lý do ngắn | 500 | Address, CancellationReason, ChangeReason |
| Mô tả, hướng dẫn | 2000 | Biography, Description, GeneralInstructions, UsageInstructions, FailureReason, ChangedColumns, ResultSummary |
| Hash mật khẩu | 512 | PasswordHash |
| Thiết bị, IP | 128 / 45 | DeviceId / DeviceIpAddress, ClientIpAddress |
| Khóa, tham chiếu ngoài | 128 | IdempotencyKey, CallerScope, OperationType (ApiIdempotencyRecord), TransactionReference, ProviderTransactionId, ProviderRefundTransactionId, EventId, SessionToken |
| SHA-256 hex | 64 | RequestHash, PayloadHash |
| Tệp | 1024 / 255 / 127 | R2StorageKey, AuthorizationProofDoc / OriginalFileName / FileContentType |
| Tên bảng audit | 128 | TableName |
| Currency | 3 | Currency (CHECK = 'VND') |
| `nvarchar(max)` | — | *Json, PayloadJson, ProviderRawData, ProviderResponsePayload, ResponseBodyJson, OldValuesJson, NewValuesJson, 8 cột văn bản lâm sàng của EncounterClinicalRecord, EncounterDiagnosis.ClinicalNotes, CLSResult.DetailedFindings |

## 5. Khóa, index và ràng buộc

### 5.1 UK
- 35 UK đơn cột theo ERD → unique index (filtered `IS NOT NULL` nếu cột nullable).
- Composite / filtered:
  - `UK (DoctorId, ServiceDate)` trên DoctorSessionSlotQuota
  - `UK (DoctorId, ServiceDate, PatientId) WHERE IsActive = 1` trên ActiveBookingClaim
  - `UK_Idem (CallerScope, OperationType, IdempotencyKey)` trên ApiIdempotencyRecord
  - `UK_ProviderEvent (Provider, EventId)` trên PaymentEvent; index thường trên PayloadHash
  - `UK (RescheduledFromAppointmentId) WHERE RescheduledFromAppointmentId IS NOT NULL` trên Appointment (1B)

### 5.2 CHECK
- 52 CHECK enum (danh sách giá trị lấy nguyên từ `note`).
- DoctorSessionSlotQuota: `HeldCount >= 0`, `AdmittedCount >= 0`, `LockedCancelledCount >= 0`, `HeldCount + AdmittedCount + LockedCancelledCount <= Capacity`.
- PriceListItem: `UnitPrice >= 0`; theo ItemType: `ConsultationFee` ⇒ `(DoctorRankId IS NOT NULL OR DoctorId IS NOT NULL) AND MedicalServiceId IS NULL`; `CLSService` ⇒ `MedicalServiceId IS NOT NULL AND DoctorRankId IS NULL AND DoctorId IS NULL`.
- PaymentAllocation: `AllocatedAmount > 0`.
- RefundRequest (4B): `(RefundReason = 'CancelledBefore0500' AND PolicyRuleApplied = 'FullRefundPre0500') OR (RefundReason <> 'CancelledBefore0500' AND PolicyRuleApplied = 'CustomApproval')`.
- Quyết định 1B/2A/3B: nằm trong CHECK enum của Appointment.Status, Encounter.Status, PaymentIntent.Provider; PaymentEvent.Provider giữ VNPT, MoMo. Ý nghĩa trạng thái Encounter (2A) ghi trong XML doc của enum, không viết logic chuyển trạng thái.

### 5.3 FK trong cùng module (46) — FK constraint + navigation phía phụ thuộc, `OnDelete(Restrict)`
- identity: Staff.AppUserId, Doctor.StaffId, PatientRepresentative.AppUserId, PatientRepresentative.PatientId, RoomAccount.AppUserId, RoomAccount.RoomId, StaffShiftAssignment.StaffId, StaffShiftAssignment.RoomId, RoomDeviceSession.RoomAccountId
- catalog: MedicalServiceRoom.MedicalServiceId, PriceListItem.PriceListId, PriceListItem.DoctorRankId, PriceListItem.MedicalServiceId
- scheduling: ActiveBookingClaim.SessionQuotaId, ActiveBookingClaim.ReservationId, ActiveBookingClaim.AppointmentId, Reservation.SessionQuotaId, Appointment.ReservationId, Appointment.SessionQuotaId, Appointment.RescheduledFromAppointmentId (self), QueueTicket.TargetStationId, ActivePatientEngagement.CurrentStationId, ActivePatientEngagement.CurrentTicketId, ActiveStationAllocation.StationId (PK = FK), ActiveStationAllocation.CurrentTicketId
- clinical: EncounterClinicalRecord.EncounterId, VitalSign.EncounterId, EncounterDiagnosis.EncounterId, ServiceOrder.EncounterId, ServiceOrderItem.ServiceOrderId, CLSResult.ServiceOrderItemId, ResultAttachment.CLSResultId, Prescription.EncounterId, PrescriptionItem.PrescriptionId
- billing: PaymentTransaction.PaymentIntentId, PaymentAllocation.PaymentTransactionId, PaymentAllocation.BillingItemId, RefundRequest.BillingItemId, RefundRequest.OriginalPaymentTransactionId, RefundTransaction.RefundRequestId, RefundTransaction.OriginalPaymentTransactionId, PaymentEvent.PaymentTransactionId
- workflow: Operation.BookingAttemptId, ApiIdempotencyRecord.OperationId, OutboxMessage.OperationId, AuditLog.OperationId

### 5.4 FK xuyên module (52) — chỉ giữ cột ID + index, không navigation, không FK constraint (MOD-01)
| Bảng (schema) | Cột → bảng đích (schema) |
|---|---|
| Doctor (identity) | SpecialtyId → Specialty, DoctorRankId → DoctorRank (catalog) |
| MedicalService (catalog) | DefaultRoomId → Room (identity) |
| MedicalServiceRoom (catalog) | RoomId → Room (identity) |
| PriceListItem (catalog) | DoctorId → Doctor (identity) |
| DoctorSessionSlotQuota (scheduling) | DoctorId → Doctor, AssignedRoomId → Room (identity) |
| ActiveBookingClaim (scheduling) | DoctorId → Doctor, PatientId → Patient (identity) |
| Reservation (scheduling) | BookingAttemptId → BookingAttempt (workflow); PatientId → Patient, AppUserId → AppUser (identity) |
| Appointment (scheduling) | PatientId → Patient (identity) |
| ServiceStation (scheduling) | RoomId → Room (identity) |
| QueueTicket (scheduling) | EncounterId → Encounter, ServiceOrderId → ServiceOrder, ServiceOrderItemId → ServiceOrderItem (clinical); TargetRoomId → Room (identity) |
| ActivePatientEngagement (scheduling) | PatientId (PK) → Patient (identity); EncounterId → Encounter (clinical) |
| ActiveStationAllocation (scheduling) | CurrentPatientId → Patient (identity) |
| Encounter (clinical) | AppointmentId → Appointment (scheduling); PatientId → Patient, OriginalDoctorId → Doctor, OriginalRoomId → Room (identity) |
| EncounterClinicalRecord (clinical) | UpdatedByDoctorId → Doctor (identity) |
| VitalSign (clinical) | RecordedByStaffId → Staff (identity) |
| ServiceOrder (clinical) | PrescribedByDoctorId → Doctor (identity) |
| ServiceOrderItem (clinical) | MedicalServiceId → MedicalService (catalog) |
| CLSResult (clinical) | PerformedByStaffId → Staff, ApprovedByStaffId → Staff (identity) |
| Prescription (clinical) | PrescribedByDoctorId → Doctor (identity) |
| PrescriptionItem (clinical) | MedicationId → MedicationCatalog (catalog) |
| BillingItem (billing) | EncounterId → Encounter, ServiceOrderItemId → ServiceOrderItem (clinical); AppointmentId → Appointment (scheduling); PriceListItemId → PriceListItem (catalog) |
| PaymentIntent (billing) | PayerAppUserId → AppUser, PatientId → Patient (identity); BookingAttemptId → BookingAttempt (workflow); EncounterId → Encounter (clinical) |
| RefundRequest (billing) | AppointmentId → Appointment (scheduling); DecisionByStaffId → Staff (identity) |
| BookingAttempt (workflow) | AppUserId → AppUser, PatientId → Patient, SelectedDoctorId → Doctor (identity); SelectedDoctorRankId → DoctorRank (catalog) |
| Operation (workflow) | InitiatedByUserId → AppUser (identity) |
| AuditLog (workflow) | PerformedByUserId → AppUser, RoomAccountId → RoomAccount, RoomDeviceSessionId → RoomDeviceSession, StaffShiftAssignmentId → StaffShiftAssignment (identity) |

### 5.5 Nullable
- 29 cột ghi "Nullable" trong ERD → nullable.
- Cột thời điểm của sự kiện chưa xảy ra, tôi tự quyết nullable (sẽ liệt kê trong báo cáo): PatientRepresentative.RevokedAtUtc, RoomDeviceSession.LogoutAtUtc, ActiveBookingClaim.ReleasedAtUtc, Appointment.CancelledAtUtc, QueueTicket.CalledAtUtc/ServedAtUtc/CompletedAtUtc, Encounter.CompletedAtUtc, ServiceOrderItem.CompletedAtUtc, CLSResult.ApprovedAtUtc/PublishedAtUtc, Prescription.IssuedAtUtc, PaymentTransaction.VerifiedAtUtc, RefundRequest.DecidedAtUtc, PaymentEvent.ProcessedAtUtc, Operation.CompletedAtUtc, OutboxMessage.ProcessedAtUtc.
- Mọi cột còn lại NOT NULL, trừ khi trưởng nhóm chọn khác ở mục 8.

## 6. Tách migrate khỏi BE và tài khoản
- BE1/BE2 không gọi `Database.Migrate()`/`EnsureCreated()` (hiện chưa có host nào; chỉ cần không thêm).
- `DbMigrator` (console): đọc connection string tài khoản migrate từ biến môi trường `SH_MIGRATOR_CONNECTION` (hoặc user-secrets khi dev), lấy `sp_getapplock` mức session, migrate 6 context theo thứ tự identity → workflow, nhả lock. Trước khi viết sẽ tra Context7 xem EF Core 10 `Migrate()` đã tự lấy lock chưa; nếu đã có thì không bọc thêm lock trùng và ghi lý do.
- `docs/db/sql/grants.sql` (để review, không chứa mật khẩu): user runtime chỉ có SELECT/INSERT/UPDATE/DELETE trên 6 schema; user migrate có quyền DDL trên 6 schema. Tạo login/mật khẩu thuộc hạ tầng của Codex, bàn giao qua handoff.
- Không ghi connection string vào `appsettings*.json` commit.

## 7. Kiểm chứng
- Test đối chiếu model (không cần DB), đọc trực tiếp `docs/db/erd-v2.2.json`: với mỗi bảng của module kiểm schema, tập cột, store type, nullability, PK, UK (đơn, ghép, filtered), FK nội bộ (Restrict), FK xuyên module (không constraint, có index), tên CHECK. Viết trước, chạy đỏ, rồi mới viết cấu hình.
- Test trên SQL Server thật (Docker, `SH_TEST_SQL_CONNECTION`): enum sai bị từ chối; `CancelledBefore0500` + `CustomApproval` bị từ chối; hai Appointment cùng `RescheduledFromAppointmentId` bị từ chối; quota vượt Capacity bị từ chối; `UnitPrice < 0`, `AllocatedAmount = 0` bị từ chối; filtered UK ActiveBookingClaim cho phép bản ghi `IsActive = 0` trùng.
- `dotnet build` không warning EF; `dotnet ef migrations script --idempotent` cho từng context ra `docs/db/sql/<module>.sql`; chạy cả 6 script hai lần liên tiếp trên DB trống trong Docker, không lỗi.
- Trước mỗi commit: `gitleaks protect --staged`, `semgrep scan --config auto` trên phần persistence; tự review theo DB-03.

## 8. Cần trưởng nhóm xác nhận
1. **Cột không phải thời điểm nhưng logic cần nullable** (mặc định theo prompt là NOT NULL; đề xuất nullable):
   - Có giá trị sau một bước nghiệp vụ: CLSResult.ApprovedByStaffId, CLSResult.PerformedAtUtc, BookingAttempt.SelectedServiceDate, Operation.FailureReason, Appointment.CancellationReason, ApiIdempotencyRecord.ResponseStatusCode/ResponseBodyJson (khi đang xử lý), PaymentTransaction.ProviderTransactionId (trước khi cổng trả về), RefundTransaction.ProviderRefundTransactionId (khi Failed), PaymentTransaction.ProviderRawData, RefundTransaction.ProviderResponsePayload.
   - Có thể không có: AppUser.Email, AppUser.PhoneNumber, Patient.NationalId (trẻ em), PatientRepresentative.AuthorizationProofDoc (Self), Doctor.Biography, Specialty.Description, PriceList.EffectiveToUtc (không hạn), PrescriptionItem.UsageInstructions, Prescription.GeneralInstructions, EncounterDiagnosis.ICD10Code/ClinicalNotes, 8 cột văn bản EncounterClinicalRecord và CLSResult.ResultSummary/DetailedFindings (lúc Draft), AuditLog.DeviceId/ClientIpAddress/ChangedColumns/OldValuesJson/NewValuesJson/ChangeReason.
2. **BillingItem.TotalAmount** ghi "UnitPrice * Quantity": đề xuất CHECK `TotalAmount = UnitPrice * Quantity` (giữ cột thường để EF ghi được).
3. **RoomDeviceSession.SessionToken**: đề xuất chỉ lưu SHA-256 của token (64 ký tự hex), không lưu token gốc; tên cột giữ nguyên.
4. **PriceListItem ConsultationFee**: cho phép có cả DoctorRankId và DoctorId cùng lúc (đúng chữ "hoặc") hay bắt buộc đúng một trong hai?
5. **SQL Server cho test**: đề xuất image `mcr.microsoft.com/mssql/server:2022-CU<mới nhất>` ghim tag cụ thể, edition Developer chỉ cho dev/test (DB-01). Production chốt sau.
6. **Ownership**: thêm `docs/db/**` và `docs/plans/**` (owner Codex) vào `docs/ai/ownership.md` trong cùng branch này?
7. **Hai bảng không vào schema module nào ở Giải pháp v2.2** (Catalog, Workflow): xác nhận tạo thư mục `backend/src/Modules/Catalog` và `backend/src/Modules/Workflow`.

## 9. Tiêu chí hoàn thành
- 46 bảng trong 6 schema khớp ERD qua test đối chiếu model, xanh.
- Test SQL Server ở mục 7 xanh, có output thật trong `docs/evidence/claude/`.
- 6 migration `InitialCreate_ErdV22`, 6 script `docs/db/sql/<module>.sql`, chạy idempotent 2 lần không lỗi.
- `DbMigrator` không phụ thuộc host BE; không có connection string trong file commit; Gitleaks và Semgrep sạch.
- Báo cáo `docs/db/erd-v2.2-implementation.md`, sơ đồ Archify trong `docs/db/`, session/evidence/handoff claude-to-codex.
- Commit theo thứ tự: ERD → từng module → migrator → báo cáo. Không push.
