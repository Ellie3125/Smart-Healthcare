# Báo cáo dựng database từ ERD v2.2 (CLA-DB-001)

- Ngày: 29/10/2026. Agent: Claude Code (Opus 5.5), làm theo quyền giám sát OWN-10 theo yêu cầu của trưởng nhóm Phạm Hoà.
- Branch `feat/db-erd-v2.2`, tách từ `main` tại `aa087a7`. Chưa push.
- Nguồn sự thật: `docs/db/erd-v2.2.json` (46 bảng, 78 quan hệ, 4 quyết định 1B · 2A · 3B · 4B). Kế hoạch đã duyệt: `docs/plans/db-erd-v2.2.md`.
- Sơ đồ 6 schema và tham chiếu ID xuyên schema: `docs/db/erd-v2.2-schemas.html` (nguồn: `docs/db/erd-v2.2-schemas.archify.json`).
- Bằng chứng chạy thật: `docs/evidence/claude/CLA-DB-001-verification.md`.

## 1. Kết quả chính

| Hạng mục | Kết quả |
|---|---|
| Bảng | 46 bảng trong 6 schema: identity 9, catalog 7, scheduling 8, clinical 10, billing 7, workflow 5 |
| DbContext | 6 context, mỗi context có `HasDefaultSchema` riêng và bảng `__EFMigrationsHistory` riêng trong schema của mình |
| FK constraint | 46, tất cả nằm trong cùng schema, `ON DELETE NO ACTION` (Restrict). Không có FK chéo schema |
| Tham chiếu xuyên schema | 52 cột chỉ giữ ID kèm index, không có navigation và không có FK constraint (MOD-01) |
| CHECK | 61 CHECK: 52 cho enum, 9 ràng buộc nghiệp vụ |
| Filtered unique index | 7 |
| Migration | `InitialCreate_ErdV22` cho từng context; script idempotent ở `docs/db/sql/<module>.sql` |
| Test | 17/17 xanh trên SQL Server 2022 CU27 trong Docker |
| Build | `dotnet build backend/SmartHealthcare.slnx` 0 warning, 0 error (bật TreatWarningsAsErrors) |

## 2. Công cụ đã dùng

| Công cụ | Bước | Cách dùng |
|---|---|---|
| Graphify | 1 | **Không có trên máy, bỏ bước.** Backend lúc bắt đầu chỉ có thư mục rỗng chứa `.gitkeep`, nên không mất thông tin về cấu trúc có sẵn |
| Superpowers | 1–7 | Theo trình tự brainstorming/writing-plans (kế hoạch được duyệt trước khi code), test-driven-development (test đỏ trước), executing-plans (làm lần lượt từng module), systematic-debugging (lỗi `QUOTED_IDENTIFIER`, lỗi ký tự trong csproj), verification-before-completion (chạy lại toàn bộ trước khi báo) |
| Context7 | 2–5 | Tra `/dotnet/entityframework.docs`: khóa migration của EF Core 9 trở lên (`sp_getapplock`), `ToTable(t => t.HasCheckConstraint(...))`, `HasFilter`, `ConfigureConventions`/`HaveConversion` |
| Ponytail | 2 | Không có generic repository, base entity hay DbSet thừa. Chỉ có một project dùng chung tối thiểu (`Shared.Persistence`) gồm converter UTC và hàm dựng CHECK cho enum |
| RTK | 3–6 | Nén output; dùng `rtk bypass` khi cần xem lỗi đầy đủ |
| security-and-hardening | 5 | Tách tài khoản migrate và runtime (`grants.sql`); connection string chỉ lấy từ biến môi trường; ghim bản vá cho phụ thuộc có lỗ hổng |
| Gitleaks | 7 | `gitleaks protect --staged` trước mỗi commit và `gitleaks detect --no-git` toàn repo: không phát hiện secret |
| Semgrep | 7 | `semgrep scan --config p/csharp --config p/secrets --metrics=off --error` trên `backend/src`, `tests/backend`, `docs/db`: 0 finding. **Không dùng `--config auto`** vì tùy chọn này bắt buộc bật gửi metrics, trái với điều cấm telemetry của project-governance |
| Archify | 8 | Sơ đồ kiến trúc 6 schema; đã qua 4 gate tự động (validate, deliver, check, browser-check), chưa có người xem trực quan |

Tên enum, entity và config được sinh bằng một script Python đọc `erd-v2.2.json`. Script chỉ nằm trong scratchpad, không commit. Code sinh ra được commit và kiểm như code viết tay; test đối chiếu tự giữ danh sách quy tắc riêng nên không phụ thuộc script.

## 3. Đối chiếu ERD ↔ EF

Test `ErdModelTests.Model_matches_erd` đọc trực tiếp `docs/db/erd-v2.2.json` và kiểm từng bảng trên design-time model:
- schema, tập cột (không thiếu, không thừa), store type, nullability, PK;
- UK đơn cột (có filter `IS NOT NULL` nếu cột nullable), UK ghép và UK có filter;
- FK nội bộ: đúng bảng đích, Restrict, có navigation;
- FK xuyên schema: không có FK, có index;
- CHECK của từng enum, so khớp đúng tập giá trị trong `note`;
- 9 CHECK nghiệp vụ.

Kết quả: **cả 46 bảng khớp**.

Test này đã được kiểm lại theo ba cách:
- Từng module đỏ trước khi có cấu hình: identity báo thiếu 9 bảng; khi bỏ thư mục `Configurations`, các module khác báo thiếu 7, 8, 10, 7 và 5 bảng.
- Cố ý làm hỏng code: đổi tên CHECK 4B và đổi Restrict thành Cascade đều bị test bắt.
- Bỏ filter của UK 1B thì test không báo lỗi. Đây không phải lỗ hổng: EF Core trên SQL Server tự thêm `WHERE ... IS NOT NULL` cho unique index trên cột nullable, nên DB sinh ra vẫn giống hệt.

Các điểm lệch có chủ đích:

| Điểm | ERD | Đã làm | Lý do |
|---|---|---|---|
| FK xuyên schema (52 cột) | Vẽ là FK | Chỉ giữ cột ID kèm index | MOD-01 cấm FK chéo schema |
| `ActivePatientEngagement.PatientId` | PK + FK tới Patient | Chỉ là PK, không có FK | Patient thuộc schema identity |
| `RoomDeviceSession.SessionToken` | string UK | `nvarchar(64)` lưu SHA-256 hex | Trưởng nhóm duyệt (kế hoạch 8.3): không lưu token gốc |
| `BillingItem.TotalAmount` | note "UnitPrice * Quantity" | CHECK `TotalAmount = UnitPrice * Quantity` | Trưởng nhóm duyệt (kế hoạch 8.2) |
| Chia module | 6 domain | Theo ERD | Trưởng nhóm chọn "Theo ERD". Lệch Giải pháp v2.2 §8.1: PriceList thuộc Billing, MedicationCatalog là module riêng, Workflow "không sở hữu dữ liệu nghiệp vụ" |

## 4. Tham chiếu xuyên schema (chỉ giữ ID, có index)

| Từ schema | Sang schema | Cột |
|---|---|---|
| identity | catalog | Doctor.SpecialtyId, Doctor.DoctorRankId |
| catalog | identity | MedicalService.DefaultRoomId, MedicalServiceRoom.RoomId, PriceListItem.DoctorId |
| scheduling | identity | DoctorSessionSlotQuota.DoctorId, DoctorSessionSlotQuota.AssignedRoomId, ActiveBookingClaim.DoctorId, ActiveBookingClaim.PatientId, Reservation.PatientId, Reservation.AppUserId, Appointment.PatientId, ServiceStation.RoomId, QueueTicket.TargetRoomId, ActivePatientEngagement.PatientId (PK), ActiveStationAllocation.CurrentPatientId (UK) |
| scheduling | clinical | QueueTicket.EncounterId, QueueTicket.ServiceOrderId, QueueTicket.ServiceOrderItemId, ActivePatientEngagement.EncounterId |
| scheduling | workflow | Reservation.BookingAttemptId |
| clinical | identity | Encounter.PatientId, Encounter.OriginalDoctorId, Encounter.OriginalRoomId, EncounterClinicalRecord.UpdatedByDoctorId, VitalSign.RecordedByStaffId, ServiceOrder.PrescribedByDoctorId, CLSResult.PerformedByStaffId, CLSResult.ApprovedByStaffId, Prescription.PrescribedByDoctorId |
| clinical | scheduling | Encounter.AppointmentId (UK) |
| clinical | catalog | ServiceOrderItem.MedicalServiceId, PrescriptionItem.MedicationId |
| billing | clinical | BillingItem.EncounterId, BillingItem.ServiceOrderItemId, PaymentIntent.EncounterId |
| billing | scheduling | BillingItem.AppointmentId, RefundRequest.AppointmentId |
| billing | catalog | BillingItem.PriceListItemId |
| billing | identity | PaymentIntent.PayerAppUserId, PaymentIntent.PatientId, RefundRequest.DecisionByStaffId |
| billing | workflow | PaymentIntent.BookingAttemptId |
| workflow | identity | BookingAttempt.AppUserId, BookingAttempt.PatientId, BookingAttempt.SelectedDoctorId, Operation.InitiatedByUserId, AuditLog.PerformedByUserId, AuditLog.RoomAccountId, AuditLog.RoomDeviceSessionId, AuditLog.StaffShiftAssignmentId |
| workflow | catalog | BookingAttempt.SelectedDoctorRankId |

Tổng: 52 cột. Ba cột đã có index nhờ PK hoặc UK (`ActivePatientEngagement.PatientId`, `ActiveStationAllocation.CurrentPatientId`, `Encounter.AppointmentId`). Một cột dẫn đầu UK ghép không lọc nên dùng luôn index đó (`DoctorSessionSlotQuota.DoctorId`). `ActiveBookingClaim.DoctorId` có thêm index thường, vì UK ghép của nó chỉ áp cho bản ghi đang `IsActive = 1`.

## 5. Precision và MaxLength cho cột ERD không ghi cụ thể

| Kiểu | Chọn | Cột |
|---|---|---|
| Tiền | `decimal(18,2)` | AppliedConsultationFee, UnitPrice (PriceListItem, BillingItem), TotalAmount, PaidAmount, RefundedAmount (BillingItem, RefundTransaction), TotalExpectedAmount, TransactionAmount, AllocatedAmount, RequestedAmount, AppliedPrice |
| Sinh hiệu | `decimal(5,2)` | WeightKg, HeightCm, SystolicBP, DiastolicBP, BodyTemperature, SpO2 |
| Enum | `nvarchar(32)` | 52 cột enum |
| Mã, số | 50 | các cột `*Code`, `*Number` |
| Tên, bản chụp | 200 | FullName, `*Name`, `*Snapshot`, DepartmentName, ActiveIngredient, Strength, Dosage, Frequency |
| Tên chẩn đoán | 500 | DiagnosisName |
| Đăng nhập | 256 | UserName, Email |
| Điện thoại, CCCD | 20 | PhoneNumber, NationalId |
| ICD-10 | 10 | ICD10Code |
| Địa chỉ, lý do ngắn | 500 | Address, CancellationReason, ChangeReason |
| Mô tả, hướng dẫn | 2000 | Biography, Description, GeneralInstructions, UsageInstructions, FailureReason, ChangedColumns, ResultSummary |
| Hash mật khẩu | 512 | PasswordHash |
| Thiết bị / IP | 128 / 45 | DeviceId / DeviceIpAddress, ClientIpAddress |
| Khóa, tham chiếu ngoài | 128 | IdempotencyKey, CallerScope, ApiIdempotencyRecord.OperationType, TransactionReference, ProviderTransactionId, ProviderRefundTransactionId, EventId, TableName |
| SHA-256 hex | 64 | RequestHash, PayloadHash, SessionToken |
| Tệp | 1024 / 255 / 127 | R2StorageKey, AuthorizationProofDoc / OriginalFileName / FileContentType |
| Tiền tệ | 3 | Currency (CHECK chỉ nhận `VND`) |
| `nvarchar(max)` | — | PayloadJson, ResponseBodyJson, OldValuesJson, NewValuesJson, ProviderRawData, ProviderResponsePayload, 8 cột văn bản của EncounterClinicalRecord, EncounterDiagnosis.ClinicalNotes, CLSResult.DetailedFindings |

`datetime` dùng `datetime2`. Khi đọc ra luôn có `Kind = Utc`; khi ghi, giá trị `Local` được đổi sang UTC, giá trị `Unspecified` được coi như UTC (xem rủi ro ở mục 9). Không đặt giá trị mặc định cho các mốc giờ ICT 06:30, 09:30, 05:00. `Capacity` cũng không có default 30 trong DB, vì giá trị này lấy từ cấu hình MaxSlotsPerSession.

## 6. Cột nullable tự quyết

- **Thời điểm của sự kiện chưa xảy ra:** PatientRepresentative.RevokedAtUtc, RoomDeviceSession.LogoutAtUtc, ActiveBookingClaim.ReleasedAtUtc, Appointment.CancelledAtUtc, QueueTicket.CalledAtUtc/ServedAtUtc/CompletedAtUtc, Encounter.CompletedAtUtc, ServiceOrderItem.CompletedAtUtc, CLSResult.ApprovedAtUtc/PublishedAtUtc, Prescription.IssuedAtUtc, PaymentTransaction.VerifiedAtUtc, RefundRequest.DecidedAtUtc, PaymentEvent.ProcessedAtUtc, Operation.CompletedAtUtc, OutboxMessage.ProcessedAtUtc.
- **Trưởng nhóm duyệt (kế hoạch 8.1):** CLSResult.ApprovedByStaffId, CLSResult.PerformedAtUtc, BookingAttempt.SelectedServiceDate, Operation.FailureReason, Appointment.CancellationReason, ApiIdempotencyRecord.ResponseStatusCode/ResponseBodyJson, PaymentTransaction.ProviderTransactionId/ProviderRawData, RefundTransaction.ProviderRefundTransactionId/ProviderResponsePayload, AppUser.Email/PhoneNumber, Patient.NationalId, PatientRepresentative.AuthorizationProofDoc, Doctor.Biography, Specialty.Description, PriceList.EffectiveToUtc, PrescriptionItem.UsageInstructions, Prescription.GeneralInstructions, EncounterDiagnosis.ICD10Code/ClinicalNotes, 8 cột văn bản của EncounterClinicalRecord, CLSResult.ResultSummary/DetailedFindings, AuditLog.DeviceId/ClientIpAddress/ChangedColumns/OldValuesJson/NewValuesJson/ChangeReason.
- **Theo ghi chú "Nullable" trong ERD:** 29 cột, xem `note` trong `erd-v2.2.json`.

## 7. CHECK và UK đã tạo

**CHECK theo 4 quyết định đã chốt:**
- 1B: `CK_Appointment_Status` gồm Scheduled, CheckedIn, Completed, Cancelled, Absent, Rescheduled. Thêm filtered UK `IX_Appointment_RescheduledFromAppointmentId` (`WHERE [RescheduledFromAppointmentId] IS NOT NULL`) và self-FK Restrict.
- 2A: `CK_Encounter_Status` gồm InProgress, WaitingCLS, ReturningDoctor, Completed, Abandoned. Ý nghĩa từng trạng thái ghi trong XML doc của enum `EncounterStatus`; chưa viết logic chuyển trạng thái.
- 3B: `CK_PaymentIntent_Provider` gồm VNPT, MoMo, Counter; `CK_PaymentEvent_Provider` chỉ gồm VNPT, MoMo.
- 4B: `CK_RefundRequest_RefundReason` (5 lý do), `CK_RefundRequest_PolicyRuleApplied`, và CHECK chéo `CK_RefundRequest_PolicyRule`: CancelledBefore0500 ⇔ FullRefundPre0500, mọi lý do khác dùng CustomApproval.

**CHECK nghiệp vụ khác:**
- `CK_DoctorSessionSlotQuota_HeldCount`, `_AdmittedCount`, `_LockedCancelledCount` (≥ 0) và `_Capacity` (tổng ba số đếm ≤ Capacity).
- `CK_PriceListItem_UnitPrice` (≥ 0) và `CK_PriceListItem_ItemTypeTarget`:
  - ConsultationFee phải có DoctorRankId hoặc DoctorId, được phép có cả hai (đã duyệt), và không có MedicalServiceId;
  - CLSService chỉ có MedicalServiceId.
- `CK_PaymentAllocation_AllocatedAmount` (> 0).
- `CK_BillingItem_TotalAmount`.

**CHECK enum:** 52 CHECK có tên dạng `CK_<Bảng>_<Cột>`, kể cả Currency = `VND`.

**UK ghép và UK có filter:**
- `IX_DoctorSessionSlotQuota_DoctorId_ServiceDate`
- `IX_ActiveBookingClaim_DoctorId_ServiceDate_PatientId WHERE [IsActive] = 1`
- `UK_Idem (CallerScope, OperationType, IdempotencyKey)`
- `UK_ProviderEvent (Provider, EventId)`, kèm index thường trên `PaymentEvent.PayloadHash`
- 35 UK đơn cột theo ERD. Năm cột trong số đó nullable nên có filter `IS NOT NULL`: AppUser.Email, AppUser.PhoneNumber, Patient.NationalId, PaymentTransaction.ProviderTransactionId, RefundTransaction.ProviderRefundTransactionId.

**Hành vi trên SQL Server thật (đã test):**
- Enum sai bị từ chối (lỗi 547).
- CancelledBefore0500 đi với CustomApproval bị từ chối; DuplicatePayment đi với FullRefundPre0500 bị từ chối.
- Hai Appointment cùng đổi từ một lịch bị từ chối (lỗi 2601).
- Quota vượt Capacity hoặc số đếm âm bị từ chối.
- PriceListItem sai loại bị từ chối.
- `TotalAmount` lệch bị từ chối; `AllocatedAmount = 0` bị từ chối.
- ActiveBookingClaim trùng chỉ bị chặn khi `IsActive = 1`.
- Rowversion phát hiện cập nhật xung đột.
- DateTime đọc ra luôn là UTC.

## 8. Migrate và review DB-03

- BE1/BE2 không gọi `Migrate()` hay `EnsureCreated()`; repo hiện chưa có host nào.
- Việc migrate do `backend/src/Tools/DbMigrator` đảm nhận: nó đọc `SH_MIGRATOR_CONNECTION` và trả exit 1 nếu thiếu biến này, rồi migrate 6 context theo thứ tự identity → workflow.
- Theo tài liệu EF Core (tra qua Context7), `MigrateAsync` từ EF Core 9 tự lấy `sp_getapplock` mức session, nên không bọc thêm khóa thứ hai. Đã chạy hai lần liên tiếp trên DB mới, cả hai đều exit 0.
- `docs/db/sql/grants.sql` định nghĩa role `sh_runtime` chỉ có DML trên 6 schema, và role `sh_migrator` có DDL. Không có login hay mật khẩu trong file; việc tạo login thuộc Codex.
- Chạy 6 script idempotent hai lần liên tiếp trên DB trống: 12/12 lần exit 0.
- **Lưu ý vận hành:** khi chạy script bằng `sqlcmd` phải thêm `-I` (bật `QUOTED_IDENTIFIER`); không có `-I` thì filtered index báo lỗi 1934. EF/SqlClient và DbMigrator đã tự bật sẵn.
- Checklist DB-03:
  - migration chạy như tác vụ riêng: đạt;
  - có lock: đạt, do EF tự lấy;
  - tài khoản riêng: có định nghĩa role, phần login chờ Codex tạo;
  - SQL có bản để review: đạt (`docs/db/sql/`);
  - không để hai replica cùng migrate: đạt;
  - expand/contract: không áp dụng cho migration đầu tiên.

## 9. Thay đổi phụ cần biết và rủi ro

- **`global.json`:** thêm mới để ghim SDK 10.0.401 và bật `test.runner = Microsoft.Testing.Platform`. Trên .NET 10, `dotnet test` không còn chạy xUnit v3 qua VSTest.
- **`Directory.Packages.props`:** ghim `System.Security.Cryptography.Pkcs` 10.0.14 qua transitive pinning. Bản 9.0.11 do `Microsoft.Data.SqlClient` kéo vào có lỗ hổng GHSA-846f-f764-76mx (high) và GHSA-2fmv-rv2c-59x7 (moderate).
- **`.gitignore`:** thêm `bin/` và `obj/`.
- **`docs/ai/ownership.md`:** thêm `docs/db/**`, `docs/plans/**`, `.config/**`, cả ba thuộc Codex.
- **Tên schema `identity`:** trùng từ khóa T-SQL `IDENTITY`. EF luôn đặt tên trong ngoặc vuông nên chạy bình thường; SQL viết tay cũng phải dùng `[identity]`.
- **Converter UTC:** coi `DateTime` có `Kind = Unspecified` là UTC. Code ứng dụng gán giờ địa phương mà không ghi rõ Kind sẽ bị lưu lệch 7 giờ.
- **TLS trong test:** chỉ test cục bộ dùng `TrustServerCertificate=True`, cho SQL Server trong Docker trên `127.0.0.1` với chứng chỉ tự ký. Môi trường thật không được dùng tùy chọn này.
- **Thư mục chưa dùng:** `backend/src/Modules/Patients`, `Documents`, `Notifications`, `Reporting` vẫn rỗng, chưa đụng tới.

## 10. Câu hỏi nhóm cần chốt

1. Cập nhật Giải pháp v2.2 §8.1 cho khớp ERD: bảng giá nằm ở catalog, MedicationCatalog gộp vào catalog, Workflow giữ BookingAttempt/Operation/Idempotency/Outbox/AuditLog. Hoặc ngược lại, sửa ERD. Việc này đã giao cho Codex qua handoff.
2. Thư mục `Patients` hiện để trống trong khi Patient nằm ở schema identity: giữ hay xóa?
3. `Reservation.IdempotencyKey` đang là UK toàn cục theo ERD, không gắn với `CallerScope` như `UK_Idem`. Có cần đổi không?
4. Thời gian sống (TTL) của `ApiIdempotencyRecord` (DB-04) và cách dọn bản ghi hết hạn.
5. Hangfire dùng schema riêng nào; tài khoản runtime có cần quyền trên schema đó không.
6. Phiên bản và edition SQL Server cho Staging/Prod (test đang dùng 2022 CU27 bản Developer).
