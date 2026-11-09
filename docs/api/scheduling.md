# Contract module Scheduling

- contract_version: **v0.1**
- Trạng thái: **Draft**, chờ trưởng nhóm duyệt (H-02). Chưa dùng để tích hợp thật trước khi Approved (API-02).
- Người giữ: Claude Code (ownership.md mục 1). Task: W1-04.
- Mã nguồn: `backend/src/Modules/Scheduling/Scheduling.Contracts/` (in-process), `Scheduling.Api/` (HTTP).
- Quy ước chung: `docs/conventions.md` v0.1 (W1-03, PR #43, chờ duyệt): ProblemDetails + extension `code`, JSON camelCase, thời điểm UTC ISO-8601 có `Z`, ngày phục vụ `yyyy-MM-dd`, header `Idempotency-Key`.
- Nguồn nghiệp vụ: Phạm vi v2.11 §4–6; Giải pháp v2.13 §1.2, §2.3, §4, §8.2; ERD v2.4 (DoctorDayQuota, Reservation, Appointment, QueueTicket).

## 1. Quy tắc suất trống

- **Suất trống của ngày D** = `max(0, Capacity − AdmittedCount − HeldCount − LockedCancelledCount)`, Capacity mặc định 30 (Phạm vi §4.3). Không bao giờ trả số âm.
- Ngày **không nhận giữ chỗ mới** khi: không có phiên; `IsOpen = false`; `SessionStatus` khác `Scheduled`/`InProgress`; giờ DB (`IDbClock`, `SYSUTCDATETIME()`) ≥ `BookingCutoffTimeUtc` (09:30 ngày D giờ Việt Nam); hoặc suất trống = 0. Tìm bác sĩ (§2.1) **không trả** các phiên thuộc bốn điều kiện đầu; phiên hết suất vẫn trả với `availableSlots = 0`.
- Ngày mai xét cutoff của ngày mai: 10:00 hôm nay vẫn đặt được ngày mai (Phạm vi §4.2).
- Tra suất trống chỉ là thông tin tham khảo. Giữ chỗ (W2-02) khóa `DoctorDayQuota` `WITH (UPDLOCK, HOLDLOCK)` **trước** khi đọc `IsOpen`, cutoff và ba bộ đếm, rồi mới đọc giờ DB và kiểm lại.

## 2. Contract trong tiến trình: `ISchedulingModule`

Lỗi nghiệp vụ ném `SchedulingException` với `SchedulingErrorCode`. Ba method chưa có bản cài đặt.

| Method | Bên gọi | Cài đặt | Unit of work | Idempotency |
|---|---|---|---|---|
| `SearchDoctorsAsync(DoctorSearchQuery)` | Endpoint tìm bác sĩ của Scheduling (W2-05); web bệnh nhân và chatbot dùng qua REST | W2-05 (Codex, PR #44 phần 1) | Không cần; chỉ đọc, không khóa | Không áp dụng |
| `CheckInAsync(CheckInAppointment)` | Endpoint tiếp tân (W3-02), đăng ký tại quầy (W3-01) | W3-02 (Codex) | Tự mở `IUnitOfWork`; lồng trong UoW của bên gọi thì tham gia | Theo `AppointmentId`: đã CheckedIn thì trả kết quả cũ, `alreadyCheckedIn = true`, không phát sự kiện lần hai |
| `CreateClsTicketsAsync(CreateClsTickets)` | Clinical khi bác sĩ chỉ định CLS | W3-04 (Claude Code) | **Bắt buộc** trong `IUnitOfWork` của Clinical, cùng đợt chỉ định và khoản phí CLS của Billing; không có transaction thì `InvalidOperationException` | Vé CLS theo `ServiceOrderItemId`, vé quay lại theo `ServiceOrderId`; gọi lặp trả vé đã có |

### 2.1 `SearchDoctorsAsync`

DTO trùng tên trường và kiểu với DTO tạm của W2-05 (PR #44, `Scheduling.Application/DoctorSearch/DoctorSearchDtos.cs`); sau khi W1-04 merge, Codex chuyển sang dùng bản trong `Scheduling.Contracts`.

- Vào: `DoctorSearchQuery(DateOnly ServiceDate, Guid? DoctorRankId = null, Guid? SpecialtyId = null, int Page = 1, int PageSize = 20)`; `Page` 1..1000 (`MaxPage`), `PageSize` 1..50 (`MaxPageSize`).
- Ra: `DoctorSearchPage(ServiceDate, Page, PageSize, HasMore, Items)`, `Items` là `DoctorSearchItem(DoctorId, FullName, DoctorRankId, SpecialtyId, AvailableSlots, ExamFee, Currency)`, sắp theo `DoctorId`.
- Chỉ phiên còn nhận đặt (§1); `AvailableSlots` ≥ 0. `ExamFee` do `IBillingModule` báo giá theo lô tại thời điểm tra; `Currency = "VND"`.
- Lỗi: `InvalidInput`. Billing chưa báo giá theo lô được thì endpoint trả 503 `ExamFeeBatchUnavailable` (§4.3).

### 2.2 `CheckInAsync`

- Vào: `CheckInAppointment(Guid AppointmentId, Guid PerformedByUserId, Guid? OperationId)`.
- Điều kiện: Appointment `Scheduled`; hôm nay là ngày D theo giờ Việt Nam; giờ DB < `CheckInDeadlineUtc` của phiên (mặc định 11:00 ngày D).
- Tác dụng: Appointment → `CheckedIn`, ghi AuditLog, phát `TicketCheckedIn` cùng transaction.
- Ra: `CheckInResult(AppointmentId, PatientId, DoctorId, ServiceDate, CheckedInAtUtc, AlreadyCheckedIn, QueueTicketId?, TicketNumber?, SequenceNumber?)`. Ba trường vé **Pending** câu hỏi mở Q1 (§6).
- Lỗi: `AppointmentNotFound`, `InvalidAppointmentState` (Cancelled, Absent, Rescheduled, Completed), `CheckInNotOpen` (chưa tới ngày D), `CheckInDeadlinePassed`.

### 2.3 `CreateClsTicketsAsync`

- Vào: `CreateClsTickets(Guid EncounterId, Guid ServiceOrderId, IReadOnlyList<ClsTicketLine> Lines, Guid? OperationId)`, `ClsTicketLine(Guid ServiceOrderItemId, Guid MedicalServiceId)`; Lines không rỗng, `ServiceOrderItemId` không trùng.
- Tác dụng: mỗi dòng một `QueueTicket` loại `CLS` ở phòng thực hiện còn hoạt động do Scheduling chọn; thêm một vé `ReturnConsultation` ở phòng ban đầu cho đợt chỉ định. Số vé cấp ngay lúc chỉ định (Phạm vi §5.1).
- Vé CLS chỉ được gọi khi khoản phí tương ứng đã trả (`PaymentVerified` với `Purpose = CLSOrderBatch`). Vé quay lại chỉ được kích hoạt khi đủ kết quả bắt buộc: Scheduling nhận `ResultPublished`, gọi contract Clinical kiểm đủ kết quả (W3-06), **không** qua method này.
- Ra: `ClsTicketsCreated(IReadOnlyList<QueueTicketDto> ClsTickets, QueueTicketDto ReturnTicket)`; `QueueTicketDto(QueueTicketId, Kind, EncounterId, ServiceOrderItemId?, TargetRoomId, TicketNumber, SequenceNumber, IssuedAtUtc)`.
- Lỗi: `InvalidInput`, `InitialTicketNotFound`, `MedicalServiceUnavailable` (dịch vụ không tồn tại, ngừng, hoặc mọi phòng đã tắt — Phạm vi §6.3).

## 3. Integration event do Scheduling phát

Theo mẫu `Billing.Contracts.PaymentVerified`: `EventId`, `Version = 1` (`CurrentVersion`), `OccurredAtUtc`, `OperationId`. Payload chỉ ID và giá trị cần để quyết định; không bệnh án, CCCD, số điện thoại (MSG-03). Phát bằng `IIntegrationEventPublisher` trong cùng transaction; bên nhận chống lặp bằng `ProcessedEvent`. `EventType`/`AggregateType` đã có sẵn trong `Shared.Infrastructure` (W1-06).

| Sự kiện | Phát khi | AggregateType / aggregateId | Bên nhận |
|---|---|---|---|
| `AppointmentConfirmed` | Xác nhận lịch từ giữ chỗ hợp lệ sau `PaymentVerified` (W2-06); đổi lịch tạo lịch mới (W2-07) | Appointment / AppointmentId | Notifications (SignalR web bệnh nhân) |
| `AppointmentCancelled` | Hủy lịch có hiệu lực (W2-07; bệnh viện hủy W3-12) | Appointment / AppointmentId | Billing (hoàn phí), Notifications |
| `TicketCheckedIn` | Check in lần đầu (W3-02) | QueueTicket / QueueTicketId (xem Q1) | Clinical (mở Encounter → `EncounterOpened`) |

Payload:

- `AppointmentConfirmed(EventId, AppointmentId, BookingAttemptId, PatientId, DoctorId, ServiceDate, AppointmentNumber, RescheduledFromAppointmentId?, OccurredAtUtc, OperationId?)`.
- `AppointmentCancelled(EventId, AppointmentId, BookingAttemptId, PatientId, DoctorId, ServiceDate, CancelledBy, Refund, SlotReleased, CancelledAtUtc, RescheduleCutoffTimeUtc, OccurredAtUtc, OperationId?)`:
  - `CancelledBy`: `Patient` | `Receptionist` | `Hospital`.
  - `Refund`: `Full` khi hủy có hiệu lực trước 05:00 ngày D (giờ DB), hoặc `CancelledBy = Hospital` ở mọi thời điểm; `None` khi bệnh nhân/tiếp tân hủy từ 05:00 (Phạm vi §6.2, §6.3). Scheduling quyết định; Billing không tự tính lại mốc.
  - Billing tìm khoản phí khám đã thu theo `BookingAttemptId`; chưa thu thì không tạo hoàn. `CancelledAtUtc` và `RescheduleCutoffTimeUtc` để đối soát.
  - `SlotReleased`: true khi trả suất (trước 05:00), false khi chuyển sang `LockedCancelledCount`.
  - Đổi lịch **không** phát sự kiện này: lịch cũ chuyển `Rescheduled`, phí chuyển sang lịch mới cùng mức phí.
- `TicketCheckedIn(EventId, AppointmentId, PatientId, DoctorId, AssignedRoomId, ServiceDate, CheckedInAtUtc, QueueTicketId?, OccurredAtUtc, OperationId?)`. Clinical mở Encounter theo `AppointmentId` (unique trong `clinical.Encounter`) và lưu `AssignedRoomId` làm phòng ban đầu.

## 4. HTTP đã có

### 4.1 Mở ngày trực (W2-01, đã merge, dev e714206)

`POST /api/scheduling/admin/doctor-days/{doctorId:guid}/{serviceDate}`

- Kênh: **Staff**. Policy `AdminOnly`. Không có body. Không nhận `Idempotency-Key` (gọi lại trả 409 `AlreadyExists` khi ngày đang mở).
- Mở ngày mới với Capacity 30, `SessionStartTimeUtc` 06:30, `BookingCutoffTimeUtc` 09:30, `RescheduleCutoffTimeUtc` 05:00, `CheckInDeadlineUtc` theo cấu hình `Scheduling:DoctorDays:CheckInDeadline` (mặc định 11:00), quy đổi từ giờ Việt Nam sang UTC. Ngày đã đóng (`isOpen = false`) thì mở lại dưới khóa `UPDLOCK, HOLDLOCK` và trả quota cũ, giữ cấu hình cũ.
- 201 + body (không có header Location):

  ```json
  { "id": "…", "doctorId": "…", "assignedRoomId": "…", "serviceDate": "2026-11-12", "capacity": 30,
    "heldCount": 0, "admittedCount": 0, "lockedCancelledCount": 0,
    "sessionStartTimeUtc": "2026-11-11T23:30:00Z", "bookingCutoffTimeUtc": "2026-11-12T02:30:00Z",
    "rescheduleCutoffTimeUtc": "2026-11-11T22:00:00Z", "checkInDeadlineUtc": "2026-11-12T04:00:00Z",
    "sessionStatus": "Scheduled", "isOpen": true }
  ```

- Lỗi: 400 `InvalidInput` · 404 `DoctorNotFound` (bác sĩ không có phòng được phân công) · 409 `AlreadyExists` · 401/403.

### 4.2 Đóng ngày trực (W2-01)

`POST /api/scheduling/admin/doctor-days/{doctorId:guid}/{serviceDate}/close`

- Kênh: **Staff**. Policy `AdminOnly`. Không có body. Gọi lặp trên ngày đã đóng trả 204.
- Khóa quota `UPDLOCK, HOLDLOCK`; từ chối khi còn bộ đếm khác 0, Reservation `Held`/`Confirmed`, hoặc Appointment khác `Cancelled`/`Rescheduled`. Giữ bản ghi quota, chỉ đặt `isOpen = false`.
- 204. Lỗi: 400 `InvalidInput` · 404 `DayNotFound` · 409 `HasBookings` · 401/403.

Ghi chú chung cho 4.1 và 4.2:

- Lỗi là ProblemDetails có extension `code` = tên `DoctorDayResult`.
- **Hai endpoint chỉ được map khi môi trường là Development** (`MapSchedulingEndpoints`), chưa lộ ở Staging/Prod.
- `IDoctorDirectory` hiện là `DevelopmentDoctorDirectory`, đọc dữ liệu giả `Scheduling:DoctorDays:DevelopmentDoctorRooms:{doctorId}` cho tới khi IdentityPatients có contract tra bác sĩ và phòng.
- Chưa có test từ chối kênh Patient (API-05); cần bổ sung khi có phiên theo kênh.

### 4.3 Tìm bác sĩ, suất trống và phí (W2-05) — theo PR #44, chưa chốt

Theo code PR #44 (`feature/w2-05-doctor-search`, 9e124cf, phần 1, chưa merge), `Scheduling.Api/DoctorSearchEndpoints.cs`:

`GET /api/scheduling/patient/doctors?serviceDate=2026-11-12&doctorRankId=&specialtyId=&page=1&pageSize=20`

- Kênh: **Patient** (vai trò `Patient`); chatbot gọi bằng phiên bệnh nhân. Chỉ map ở Development.
- Query: `serviceDate` bắt buộc; `doctorRankId`, `specialtyId` tùy chọn; `page` 1..1000 (mặc định 1); `pageSize` 1..50 (mặc định 20).
- 200:

  ```json
  { "serviceDate": "2026-11-12", "page": 1, "pageSize": 20, "hasMore": false,
    "items": [ { "doctorId": "…", "fullName": "…", "doctorRankId": "…", "specialtyId": "…",
                 "availableSlots": 12, "examFee": 150000, "currency": "VND" } ] }
  ```

- Lỗi: 400 (ngày, bộ lọc hoặc phân trang không hợp lệ) · 401/403 · 503 `ExamFeeBatchUnavailable` (Billing chưa có báo giá theo lô; phần 1 dùng fake).
- Danh sách bác sĩ đang lấy từ `DevelopmentDoctorSearchDirectory` (dữ liệu giả) cho tới khi IdentityPatients có contract.
- Điểm cần sửa trước khi chốt (gửi Codex): (1) so cutoff bằng `DateTime.UtcNow` của máy BE, cần dùng giờ DB (`IDbClock`) theo §1; (2) 400 chưa có extension `code` = `InvalidInput` (docs/conventions.md §4).

## 5. HTTP dự kiến của W2 — chưa chốt

Chỉ route và mục đích, để Antigravity (W2-09) mock trước. Body, mã lỗi và quyền chốt trong bản contract sau. Kênh **Patient** trừ khi ghi khác. Mọi lệnh thay đổi nhận `Idempotency-Key`.

| Method + route | Mục đích | Task |
|---|---|---|
| `POST /api/scheduling/booking-attempts` | Tạo tiến trình đặt lịch (BookingAttempt) cho một hồ sơ bệnh nhân được đại diện | W2-03 |
| `GET /api/scheduling/booking-attempts/{bookingAttemptId}` | Khôi phục tiến trình: trạng thái, bước đã xong, `expiresAtUtc`, `allowedActions` (Giải pháp §2.3) | W2-04 |
| `POST /api/scheduling/booking-attempts/{bookingAttemptId}/hold` | Giữ chỗ bác sĩ + ngày (5 phút) | W2-02 |
| `POST /api/scheduling/booking-attempts/{bookingAttemptId}/payment` | Tạo yêu cầu thanh toán phí khám (gọi `IBillingModule.CreatePaymentAsync`), trả liên kết VNPAY | W2-02/W2-06 |
| `GET /api/scheduling/operations/{operationId}` | Tra lệnh dài (202 + `operationId`) | W2-03 |
| `GET /api/scheduling/appointments` | Lịch hẹn của các hồ sơ người dùng được đại diện | W2-07 |
| `GET /api/scheduling/appointments/{appointmentId}` | Chi tiết lịch hẹn, mã lịch hẹn, số thứ tự (sau check in) | W2-07 |
| `POST /api/scheduling/appointments/{appointmentId}/reschedule` | Đổi lịch trước 05:00 sang bác sĩ cùng mức phí | W2-07 |
| `GET /api/scheduling/appointments/{appointmentId}/cancellation-preview` | Số tiền được hoàn / không hoàn trước khi xác nhận hủy | W2-07 |
| `POST /api/scheduling/appointments/{appointmentId}/cancel` | Hủy lịch (bệnh nhân: chỉ Scheduled) | W2-07 |
| `POST /api/scheduling/appointments/{appointmentId}/check-in` | Tiếp tân check in — kênh **Staff** | W3-02 |

## 6. Câu hỏi mở

| # | Câu hỏi | Ảnh hưởng tới contract | Trạng thái |
|---|---|---|---|
| Q1 | Vé khám đầu có trước hay sau Encounter? ERD v2.4 có `QueueTicket.EncounterId` bắt buộc, nhưng Encounter do Clinical mở sau `TicketCheckedIn`. | Nếu vé có trước: `EncounterId` phải nullable (migration) hoặc Scheduling cấp EncounterId trước. Nếu vé sau: `CheckInResult` và `TicketCheckedIn` không có `QueueTicketId`, Scheduling cấp vé khi nhận `EncounterOpened`; aggregate của `TicketCheckedIn` (đang ánh xạ QueueTicket) phải đổi sang Appointment. Contract v0.1 để các trường vé nullable. | **Pending** (PROGRESS mục 4, cần trưởng nhóm chốt trước W3-02) |
| Q2 | API tìm bác sĩ có bản Staff cho tiếp tân không, hay tiếp tân dùng chung endpoint Patient? | Thêm route Staff hoặc mở endpoint cho hai kênh (API-05 yêu cầu khai kênh). | Mở |
| Q3 | Bệnh viện hủy sau 05:00 (bác sĩ nghỉ): trả suất hay khóa suất? Hiện chỉ quy định hoàn phí. | Giá trị `SlotReleased` của `AppointmentCancelled` khi `CancelledBy = Hospital`. | Mở |
| Q4 | Có phát sự kiện riêng khi đổi lịch (cho Notifications) ngoài `AppointmentConfirmed` của lịch mới không? | Hiện chỉ phát `AppointmentConfirmed` có `RescheduledFromAppointmentId`. | Mở |
| Q5 | Giới hạn số ngày tới được mở lịch (khoảng ngày được mở, Phạm vi §4.2) là bao nhiêu? | Validation `serviceDate` ở tìm bác sĩ và giữ chỗ. | Mở |
| Q6 | Phân trang: PR #44 dùng `hasMore`, docs/conventions.md v0.1 (PR #43) ghi `totalCount`. Chọn cách nào làm chuẩn? | Response tìm bác sĩ và mọi danh sách sau này; sửa một trong hai PR. | Mở |
| Q7 | Route theo kênh: PR #44 đặt `/api/scheduling/patient/doctors`. Có lấy tiền tố `/patient/` và `/staff/` làm quy ước kênh không? | Các route §5 (hiện chưa có tiền tố kênh) và docs/conventions.md §3. | Mở |

## 7. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 | 09/11/2026 | Bản đầu (W1-04): `ISchedulingModule` (tìm bác sĩ theo DTO của PR #44, check in, vé CLS + vé quay lại), 3 sự kiện, HTTP W2-01, W2-05 theo PR #44, danh sách endpoint W2 dự kiến |
