# Contract module Scheduling

- contract_version: **v0.11** (Draft, chờ trưởng nhóm duyệt; v0.4 Approved 12/11/2026)
- Trạng thái: **Approved** (H-02, trưởng nhóm duyệt 10/11/2026). Q1 đã chốt 17/11/2026; Q2–Q5 ở §6 vẫn là đề xuất cho tới khi trưởng nhóm chốt từng câu.
- Người giữ: theo ownership.md mục 1. Task: W1-04.
- Mã nguồn: `backend/src/Modules/Scheduling/Scheduling.Contracts/` (in-process), `Scheduling.Api/` (HTTP).
- Quy ước chung: `docs/conventions.md` v0.1 (W1-03, PR #43, chờ duyệt): ProblemDetails + extension `code`, JSON camelCase, thời điểm UTC ISO-8601 có `Z`, ngày phục vụ `yyyy-MM-dd`, header `Idempotency-Key`.
- Nguồn nghiệp vụ: Phạm vi v2.12 §4–6; Giải pháp v2.14 §1.2, §2.3, §4, §8.2; ERD v2.4 (DoctorDayQuota, Reservation, Appointment, QueueTicket).

## 1. Quy tắc suất trống

- **Suất trống của ngày D** = `max(0, Capacity − AdmittedCount − HeldCount − LockedCancelledCount)`, Capacity mặc định 30 (Phạm vi §4.3). Không bao giờ trả số âm.
- Ngày **không nhận giữ chỗ mới** khi: không có phiên; `IsOpen = false`; `SessionStatus` khác `Scheduled`/`InProgress`; giờ DB (`IDbClock`, `SYSUTCDATETIME()`) ≥ `BookingCutoffTimeUtc` (09:30 ngày D giờ Việt Nam); hoặc suất trống = 0. Tìm bác sĩ (§2.1) **không trả** các phiên thuộc bốn điều kiện đầu; phiên hết suất vẫn trả với `availableSlots = 0`.
- Ngày mai xét cutoff của ngày mai: 10:00 hôm nay vẫn đặt được ngày mai (Phạm vi §4.2).
- Khoảng ngày được đặt (Q5, đề xuất, chờ trưởng nhóm duyệt): không có cấu hình số ngày. Chỉ đặt được ngày admin đã mở (`IsOpen = true`) và `serviceDate` ≥ hôm nay theo giờ DB quy đổi sang giờ Việt Nam; admin kiểm soát giới hạn bằng việc mở ngày.
- Tra suất trống chỉ là thông tin tham khảo. Giữ chỗ (W2-02) khóa `DoctorDayQuota` `WITH (UPDLOCK, HOLDLOCK)` **trước** khi đọc `IsOpen`, cutoff và ba bộ đếm, rồi mới đọc giờ DB và kiểm lại.

## 2. Contract trong tiến trình: `ISchedulingModule`

Lỗi nghiệp vụ ném `SchedulingException` với `SchedulingErrorCode`. Ba method chưa có bản cài đặt.

| Method | Bên gọi | Cài đặt | Unit of work | Idempotency |
|---|---|---|---|---|
| `SearchDoctorsAsync(DoctorSearchQuery)` | Endpoint tìm bác sĩ của Scheduling (W2-05); web bệnh nhân và chatbot dùng qua REST | W2-05 (PR #44 phần 1, đã merge 80a563a) | Không cần; chỉ đọc, không khóa | Không áp dụng |
| `CheckInAsync(CheckInAppointment)` | Endpoint tiếp tân (W3-02), đăng ký tại quầy (W3-01) | W3-02 | Tự mở `IUnitOfWork`; lồng trong UoW của bên gọi thì tham gia | Theo `AppointmentId`: đã CheckedIn thì trả kết quả cũ, `alreadyCheckedIn = true`, không phát sự kiện lần hai |
| `CreateClsTicketsAsync(CreateClsTickets)` | Clinical khi bác sĩ chỉ định CLS | W3-04 | **Bắt buộc** trong `IUnitOfWork` của Clinical, cùng đợt chỉ định và khoản phí CLS của Billing; không có transaction thì `InvalidOperationException` | Vé CLS theo `ServiceOrderItemId`, vé quay lại theo `ServiceOrderId`; gọi lặp trả vé đã có |

### 2.1 `SearchDoctorsAsync`

DTO trùng tên trường và kiểu với DTO tạm của W2-05 (PR #44, `Scheduling.Application/DoctorSearch/DoctorSearchDtos.cs`); W2-05 phần 2 chuyển sang dùng bản trong `Scheduling.Contracts`.

- Vào: `DoctorSearchQuery(DateOnly ServiceDate, Guid? DoctorRankId = null, Guid? SpecialtyId = null, int Page = 1, int PageSize = 20)`; `Page` 1..1000 (`MaxPage`), `PageSize` 1..50 (`MaxPageSize`).
- Ra: `DoctorSearchPage(ServiceDate, Page, PageSize, HasMore, Items)`, `Items` là `DoctorSearchItem(DoctorId, FullName, DoctorRankId, SpecialtyId, AvailableSlots, ExamFee, Currency, TotalSlots, DoctorRankName, SpecialtyName)`, sắp theo `DoctorId`. `TotalSlots` là `Capacity` của phiên; `DoctorRankName`, `SpecialtyName` để hiển thị, null khi danh bạ bác sĩ không cung cấp (v0.4).
- Chỉ phiên còn nhận đặt (§1); `AvailableSlots` ≥ 0. `ExamFee` do `IBillingModule` báo giá theo lô tại thời điểm tra; `Currency = "VND"`.
- Lỗi: `InvalidInput`. Billing chưa báo giá theo lô được thì endpoint trả 503 `ExamFeeBatchUnavailable` (§4.3).

### 2.2 `CheckInAsync`

- Vào: `CheckInAppointment(Guid AppointmentId, Guid PerformedByUserId, Guid? OperationId)`.
- Điều kiện: Appointment `Scheduled`; hôm nay là ngày D theo giờ Việt Nam; giờ DB < `CheckInDeadlineUtc` của phiên (mặc định 11:00 ngày D).
- Tác dụng (Q1 đã chốt): trong cùng một `IUnitOfWork`, khóa `DoctorDayQuota` rồi Appointment; gọi `IClinicalModule.StartEncounterAsync` đồng bộ để mở Encounter; tạo `QueueTicket` khám đầu `Waiting` với `EncounterId` đó; Appointment → `CheckedIn`; ghi AuditLog; phát `TicketCheckedIn`. Lỗi ở bất kỳ bước nào (kể cả Clinical) rollback toàn bộ. Host phải đăng ký `ClinicalDbContext` làm participant UoW.
- Số thứ tự: `SequenceNumber` = MAX của phiên (bác sĩ, ngày) + 1 dưới khóa quota; `TicketNumber = <quotaId:N>-<sequence:D3>` (mã nội bộ, có phạm vi phiên); UI hiển thị `SequenceNumber`.
- Replay: Appointment đã `CheckedIn` thì trả vé cũ với `AlreadyCheckedIn = true`, không mở Encounter lần hai.
- Ra: `CheckInResult(AppointmentId, PatientId, DoctorId, ServiceDate, CheckedInAtUtc, AlreadyCheckedIn, QueueTicketId, TicketNumber, SequenceNumber, EncounterId)`.
- Lỗi: `AppointmentNotFound`, `InvalidAppointmentState` (Cancelled, Absent, Rescheduled, Completed, phiên đóng, hoặc bệnh nhân đang có Encounter dở), `CheckInNotOpen` (chưa tới ngày D), `CheckInDeadlinePassed` (lịch chuyển `Absent` cùng lúc).
- Job `CheckInDeadlineJob` (Worker, mỗi phút): phiên đã qua hạn thì lịch `Scheduled` → `Absent`; không giảm `AdmittedCount`, không hoàn phí.

### 2.3 `CreateClsTicketsAsync`

- Vào: `CreateClsTickets(Guid EncounterId, Guid ServiceOrderId, IReadOnlyList<ClsTicketLine> Lines, Guid? OperationId)`, `ClsTicketLine(Guid ServiceOrderItemId, Guid MedicalServiceId)`; Lines không rỗng, `ServiceOrderItemId` không trùng.
- Tác dụng: mỗi dòng một `QueueTicket` loại `CLS` ở phòng thực hiện còn hoạt động do Scheduling chọn; thêm một vé `ReturnConsultation` ở phòng ban đầu cho đợt chỉ định. Số vé cấp ngay lúc chỉ định (Phạm vi §5.1).
- Vé CLS chỉ được gọi khi khoản phí tương ứng đã trả (`PaymentVerified` với `Purpose = CLSOrderBatch`). Vé quay lại chỉ được kích hoạt khi đủ kết quả bắt buộc: Scheduling nhận `ResultPublished`, gọi contract Clinical kiểm đủ kết quả (W3-06), **không** qua method này.
- Hiện thực (W3-04, v0.7): chạy trong `IUnitOfWork` của bên gọi (Clinical lỗi thì vé rollback cùng). Số vé cấp từ bảng đếm `scheduling.TicketSequence` theo phạm vi (tiền tố số vé), khóa dòng đếm tới commit. Phòng thực hiện: `MedicalServiceRoom` đang bật của dịch vụ đang hoạt động, phòng mặc định trước, phòng phải đang hoạt động ở Identity. Số vé CLS `<roomId:N>-<yyyyMMdd>-<seq:D3>` theo phòng và ngày Việt Nam; vé quay lại `<quotaId:N>-R<seq:D3>` trong phiên của vé khám đầu. Gửi lại cùng `ServiceOrderId` và cùng các dòng trả đúng vé đã có; khác nội dung → `InvalidInput`.
- Vé quay lại tạo ở `Deferred` (tạm hoãn tới khi đủ kết quả). Kích hoạt (W3-06; sửa 17/11/2026, CLA-PERF-001): `Deferred` → `Waiting` và ghi `ActivatedAtUtc` = thời điểm kích hoạt; `IssuedAtUtc` giữ giờ phát. `QueuedAtUtc` (cột tính ở DB) = `ActivatedAtUtc` nếu có, ngược lại `IssuedAtUtc`; nhóm quay lại xếp theo `QueuedAtUtc`.
- Ra: `ClsTicketsCreated(IReadOnlyList<QueueTicketDto> ClsTickets, QueueTicketDto ReturnTicket)`; `QueueTicketDto(QueueTicketId, Kind, EncounterId, ServiceOrderItemId?, TargetRoomId, TicketNumber, SequenceNumber, IssuedAtUtc)`.
- Lỗi: `InvalidInput`, `InitialTicketNotFound`, `MedicalServiceUnavailable` (dịch vụ không tồn tại, ngừng, hoặc mọi phòng đã tắt — Phạm vi §6.3).

## 3. Integration event do Scheduling phát

Theo mẫu `Billing.Contracts.PaymentVerified`: `EventId`, `Version = 1` (`CurrentVersion`), `OccurredAtUtc`, `OperationId`. Payload chỉ ID và giá trị cần để quyết định; không bệnh án, CCCD, số điện thoại (MSG-03). Phát bằng `IIntegrationEventPublisher` trong cùng transaction; bên nhận chống lặp bằng `ProcessedEvent`. `EventType`/`AggregateType` đã có sẵn trong `Shared.Infrastructure` (W1-06).

| Sự kiện | Phát khi | AggregateType / aggregateId | Bên nhận |
|---|---|---|---|
| `AppointmentConfirmed` | Xác nhận lịch từ giữ chỗ hợp lệ sau `PaymentVerified` (W2-06); đổi lịch tạo lịch mới (W2-07) | Appointment / AppointmentId | Notifications (SignalR web bệnh nhân) |
| `AppointmentCancelled` | Hủy lịch có hiệu lực (W2-07; bệnh viện hủy W3-12) | Appointment / AppointmentId | Billing (hoàn phí), Notifications |
| `TicketCheckedIn` | Check in lần đầu (W3-02) | QueueTicket / QueueTicketId | Không có handler trong MVP (Encounter đã mở đồng bộ, Q1); Worker đăng ký event để dispatcher đánh dấu xử lý |
| `QueueCalled` | Vé chuyển Waiting → Called thành công (gọi người tiếp theo; gọi lặp trả vé đang gọi thì không phát; trả về hàng chờ rồi gọi lại phát EventId mới) (W4-02) | QueueTicket / QueueTicketId | Notifications: `QueueUpdated` tới người bấm và nhóm phòng, `DisplayBoardUpdated` tới màn hình chờ |
| `ClsTicketCancelled` | Job cuối ngày hủy vé CLS chưa thực hiện của ngày đã qua (W3-13) | QueueTicket / QueueTicketId | Chưa có handler (Billing tự hủy phí CLS chưa trả bằng job riêng); Worker đăng ký để dispatcher đánh dấu xử lý |

Payload:

- `AppointmentConfirmed(EventId, AppointmentId, BookingAttemptId, PatientId, DoctorId, ServiceDate, AppointmentNumber, RescheduledFromAppointmentId?, OccurredAtUtc, OperationId?)`.
- `AppointmentCancelled(EventId, AppointmentId, BookingAttemptId, PatientId, DoctorId, ServiceDate, CancelledBy, Refund, SlotReleased, CancelledAtUtc, RescheduleCutoffTimeUtc, OccurredAtUtc, OperationId?)`:
  - `CancelledBy`: `Patient` | `Receptionist` | `Hospital`.
  - `Refund`: `Full` khi hủy có hiệu lực trước 05:00 ngày D (giờ DB), hoặc `CancelledBy = Hospital` ở mọi thời điểm; `None` khi bệnh nhân/tiếp tân hủy từ 05:00 (Phạm vi §6.2, §6.3). Scheduling quyết định; Billing không tự tính lại mốc.
  - Billing tìm khoản phí khám đã thu theo `BookingAttemptId`; chưa thu thì không tạo hoàn. `CancelledAtUtc` và `RescheduleCutoffTimeUtc` để đối soát.
  - `SlotReleased`: true khi trả suất (`AdmittedCount − 1`), false khi chuyển sang `LockedCancelledCount`. Bệnh nhân/tiếp tân hủy: true trước 05:00, false từ 05:00.
  - Bệnh viện hủy (Q3, đề xuất, chờ trưởng nhóm duyệt): ở **bất kỳ giờ nào** cũng `SlotReleased = true` (`AdmittedCount − 1`, không tăng `LockedCancelledCount`) và `Refund = Full`; bệnh nhân không có lỗi nên không bị khóa suất.
  - Đổi lịch **không** phát sự kiện này: lịch cũ chuyển `Rescheduled`, phí chuyển sang lịch mới cùng mức phí. Q4 (đề xuất, chờ trưởng nhóm duyệt): MVP không có sự kiện riêng cho đổi lịch; Notifications nhận `AppointmentConfirmed` có `RescheduledFromAppointmentId`.
- `QueueCalled(EventId, QueueTicketId, EncounterId, PatientId, RoomId, StationId, TicketNumber, SequenceNumber, TicketType, PerformedByUserId, OccurredAtUtc, OperationId?)`, Version 1. Ghi Outbox cùng transaction với allocation và audit; publisher lỗi thì rollback cả trạng thái Called.
- `ClsTicketCancelled(EventId, QueueTicketId, EncounterId, PatientId, ServiceOrderItemId, Reason = EndOfServiceDay, OccurredAtUtc, OperationId?)`, Version 1. Mốc cuối ngày theo giờ Việt Nam tính từ giờ DB.
- Job Worker (17/11/2026): `ClsDayEndJob` 00:05 giờ Việt Nam (chạy bù đúng sau downtime); `SessionCompletionJob` mỗi 5 phút kết thúc phiên đã bắt đầu khi mọi lịch hẹn ở trạng thái cuối, không đóng theo giờ. `ICoordinationExceptionReader.ListAsync(take 1..100)` liệt kê ngoại lệ điều phối (bác sĩ/phòng ban đầu ngừng hoạt động) cho trang giám sát.
- `TicketCheckedIn(EventId, AppointmentId, PatientId, DoctorId, AssignedRoomId, ServiceDate, CheckedInAtUtc, QueueTicketId, OccurredAtUtc, OperationId?, EncounterId)`, Version 1. Encounter đã mở trong cùng transaction check-in (Q1); Clinical không nhận sự kiện này.

## 4. HTTP đã có

### 4.1 Mở ngày trực (W2-01, đã merge, dev e714206)

`POST /api/scheduling/admin/doctor-days/{doctorId:guid}/{serviceDate}`

- Kênh: **Staff**. Policy `AdminOnly` (vai trò `Admin` và claim `channel` = `Staff`, FIX-013). Body `{ "roomId": "<guid>" }` **tùy chọn** (v0.10, trưởng nhóm chốt 17/11/2026): có `roomId` thì dùng phòng admin chọn; không có (body rỗng hoặc không gửi body) thì lấy phòng theo ca trực của bác sĩ trong ngày (`StaffShiftAssignment` qua Identity §10); bác sĩ không có ca, hoặc các ca trong ngày ở nhiều phòng khác nhau → 400 `RoomRequired`. Client cũ luôn gửi `roomId` vẫn chạy như trước. Không nhận `Idempotency-Key` (gọi lại trả 409 `AlreadyExists` khi ngày đang mở).
- Kiểm qua `IIdentityPatientsModule`: bác sĩ tồn tại và đang hoạt động; phòng tồn tại, đang hoạt động và `RoomType = Clinical`.
- Mở ngày mới với Capacity 30, `SessionStartTimeUtc` 06:30, `BookingCutoffTimeUtc` 09:30, `RescheduleCutoffTimeUtc` 05:00, `CheckInDeadlineUtc` theo cấu hình `Scheduling:DoctorDays:CheckInDeadline` (mặc định 11:00), quy đổi từ giờ Việt Nam sang UTC. Ngày đã đóng (`isOpen = false`) thì mở lại dưới khóa `UPDLOCK, HOLDLOCK` và trả quota cũ, giữ cấu hình cũ.
- 201 + body (không có header Location):

  ```json
  { "id": "…", "doctorId": "…", "assignedRoomId": "…", "serviceDate": "2026-11-12", "capacity": 30,
    "heldCount": 0, "admittedCount": 0, "lockedCancelledCount": 0,
    "sessionStartTimeUtc": "2026-11-11T23:30:00Z", "bookingCutoffTimeUtc": "2026-11-12T02:30:00Z",
    "rescheduleCutoffTimeUtc": "2026-11-11T22:00:00Z", "checkInDeadlineUtc": "2026-11-12T04:00:00Z",
    "sessionStatus": "Scheduled", "isOpen": true }
  ```

- Lỗi: 400 `InvalidInput` (ngày sai) · 400 `RoomRequired` (không gửi phòng và không suy được từ ca trực) · 404 `DoctorNotFound` (bác sĩ không có hoặc ngừng hoạt động) · 404 `RoomNotFound` (phòng không có, ngừng, hoặc không phải phòng khám) · 409 `AlreadyExists` · 401/403.

`GET /api/scheduling/admin/doctor-days?serviceDate=yyyy-MM-dd` (v0.5)

- Kênh **Staff**, `AdminOnly`. `serviceDate` bắt buộc, định dạng `yyyy-MM-dd`; sai → 400 `InvalidInput`.
- 200: mảng (không phân trang, tối đa số bác sĩ của một ngày), mỗi phần tử:

  ```json
  { "id": "…", "doctorId": "…", "doctorName": "…", "doctorRankName": "…", "specialtyName": "…",
    "roomId": "…", "roomCode": "PK01", "roomName": "…", "serviceDate": "2026-11-18", "totalSlots": 30,
    "heldCount": 0, "onlineBookedCount": 3, "walkInBookedCount": 1, "lockedCancelledCount": 0,
    "remainingSlots": 26, "status": "Open", "sessionStatus": "Scheduled" }
  ```

- `onlineBookedCount`/`walkInBookedCount`: lịch hẹn còn hiệu lực (claim đang giữ) theo nguồn web/chatbot hoặc quầy. `remainingSlots = max(0, totalSlots − held − admitted − lockedCancelled)`. Tên bác sĩ/phòng đọc từ Identity; không còn trong Identity thì để `null`. `status` = `Open` | `Closed` theo `isOpen`.

### 4.2 Đóng ngày trực (W2-01)

`POST /api/scheduling/admin/doctor-days/{doctorId:guid}/{serviceDate}/close`

- Kênh: **Staff**. Policy `AdminOnly` (như 4.1). Không có body. Gọi lặp trên ngày đã đóng trả 204.
- Khóa quota `UPDLOCK, HOLDLOCK`; từ chối khi còn bộ đếm khác 0, Reservation `Held`/`Confirmed`, hoặc Appointment khác `Cancelled`/`Rescheduled`. Giữ bản ghi quota, chỉ đặt `isOpen = false`.
- 204. Lỗi: 400 `InvalidInput` · 404 `DayNotFound` · 409 `HasBookings` · 401/403.

Ghi chú chung cho 4.1 và 4.2:

- Lỗi là ProblemDetails có extension `code` = tên `DoctorDayResult`.
- Từ v0.5 (17/11/2026) mọi route Scheduling được map ở mọi môi trường; danh bạ bác sĩ (`IDoctorDirectory`, `IDoctorSearchDirectory`) đọc thật qua `IIdentityPatientsModule`. Lịch khám demo do lệnh `seed` của Api tạo (`dotnet SmartHealthcare.Api.dll seed`), không phải dữ liệu giả trong cấu hình.
- Test từ chối kênh sai (API-05): `AuthorizationMatrixTests` gọi HTTP endpoint mở ngày với mọi vai trò khác và với Admin mang `channel` = `Patient`, đều 403; `EndpointAuthorizationInventoryTests` bắt endpoint thiếu policy vai trò + kênh.

### 4.3 Tìm bác sĩ, suất trống và phí (W2-05 phần 1, đã merge)

Theo code trên `dev` (PR #44 merge 80a563a, policy sửa trong PR #54), `Scheduling.Api/DoctorSearchEndpoints.cs`:

`GET /api/scheduling/patient/doctors?serviceDate=2026-11-12&doctorRankId=&specialtyId=&page=1&pageSize=20`

- Kênh: **Patient**. Policy `PatientOnly` (vai trò `Patient` và claim `channel` = `Patient`); chatbot gọi bằng phiên bệnh nhân. Chỉ map ở Development.
- Query: `serviceDate` bắt buộc; `doctorRankId`, `specialtyId` tùy chọn; `page` 1..1000 (mặc định 1); `pageSize` 1..50 (mặc định 20).
- 200:

  ```json
  { "serviceDate": "2026-11-12", "page": 1, "pageSize": 20, "hasMore": false,
    "items": [ { "doctorId": "…", "fullName": "…", "doctorRankId": "…", "specialtyId": "…",
                 "availableSlots": 12, "examFee": 150000, "currency": "VND",
                 "totalSlots": 30, "doctorRankName": "Bác sĩ Chuyên khoa I", "specialtyName": "Khoa Hô hấp" } ] }
  ```

- Lỗi: 400 `InvalidInput` (ngày, bộ lọc hoặc phân trang không hợp lệ) · 401/403.
- Mốc cutoff so bằng giờ DB: `nowUtc` đọc một lần qua `IDbClock`, dùng chung cho lọc `BookingCutoffTimeUtc > nowUtc` và `QuoteExamFee.AtUtc`. Phí lấy qua `IBillingModule.QuoteExamFeeAsync` từng bác sĩ trong trang (tối đa 50; đổi sang báo giá theo lô khi Billing có).
- Danh sách bác sĩ đang lấy từ `DevelopmentDoctorSearchDirectory` (dữ liệu giả) cho tới khi IdentityPatients có contract.

### 4.4 Khôi phục tiến trình đặt lịch (W2-04, PR #55 đã merge 93e59e0)

Theo đề nghị OWN-14 trong mô tả PR #55 (HEAD 6b9e776). Ba route chỉ map ở Development; policy `PatientOnly` (vai trò `Patient` + claim `channel` = `Patient`). Thiếu xác thực trả 401, sai policy trả 403. Tiến trình, lệnh không tồn tại, không thuộc người gọi hoặc người gọi mất quyền Booking trên hồ sơ đều trả cùng 404.

`GET /api/scheduling/patient/booking-attempts/{bookingAttemptId:guid}`

- 200: `{ "bookingAttemptId", "status", "currentStep", "expiresAtUtc" (nullable), "allowedActions": [...] }`. Enum dạng chuỗi tên.
- Hết hạn so bằng giờ DB (`IDbClock`): attempt `Held`/`WaitingPayment` mà reservation mới nhất không còn `Held` hoặc `expiresAtUtc <= nowUtc` thì trả `status = NeedsReselection`, `currentStep = DoctorSelection`, kể cả khi worker chưa cập nhật hàng.
- `allowedActions`: trạng thái đã lưu `Draft` hoặc `NeedsReselection` → `["Hold"]` (Giải pháp §2.3: chọn lại lịch); `Held`/`WaitingPayment` còn hạn → `["ContinuePayment"]`; trạng thái khác → `[]`.
- Hết hạn phát hiện lúc đọc (hàng còn `Held`/`WaitingPayment`, worker chưa lưu `NeedsReselection`) trả `NeedsReselection`, `DoctorSelection`, `allowedActions = []`, vì lệnh giữ chỗ chỉ nhận trạng thái đã lưu `Draft`/`NeedsReselection`. Client đọc lại sau khi worker cập nhật.
- Lỗi: 404 `AttemptNotFound`.

`GET /api/scheduling/patient/operations/{operationId:guid}`

- Chỉ Operation do người gọi khởi tạo và gắn với một BookingAttempt người gọi được xem.
- 200: `{ "operationId", "bookingAttemptId", "operationType", "status", "bookingAttemptStatus", "currentStep", "expiresAtUtc", "allowedActions", "createdAtUtc", "completedAtUtc" (nullable) }`. Không trả lý do lỗi nội bộ.
- Lỗi: 404 `OperationNotFound`.

`POST /api/scheduling/patient/booking-attempts/{bookingAttemptId:guid}/hold`

- Header `Idempotency-Key` bắt buộc (một giá trị, tối đa 64 ký tự). Body JSON `{ "doctorId": "<uuid>", "serviceDate": "yyyy-MM-dd" }`. Hạng bác sĩ lấy phía server, không nhận từ client.
- 201 `{ "operationId", "reservationId", "expiresAtUtc" }` khi giữ chỗ đã commit; 202 `{ "operationId" }` khi lệnh cùng khóa đang xử lý và đã có OperationId. Cả hai có header `Location: /api/scheduling/patient/operations/{operationId}`.
- Gửi lại cùng người gọi, khóa và nội dung trả kết quả đã lưu với cùng `operationId`, không tạo lệnh mới.
- Lỗi: 400 `InvalidInput` · 415 `UnsupportedMediaType` (body không phải JSON) · 404 `AttemptNotFound`, `DoctorNotFound`, `DayNotFound` · 409 `IdempotencyConflict`, `RequestInProgress` (đang xử lý, chưa có OperationId), `InvalidAttemptState`, `DayClosed`, `CutoffPassed`, `Full`, `AlreadyClaimed`.

### 4.5 Đặt khám lần đầu: tạo tiến trình, tạo thanh toán, xem lịch hẹn (12/11/2026)

Bốn route chỉ map ở Development; policy `PatientOnly`. Thứ tự gọi của web bệnh nhân: tìm bác sĩ (§4.3) → tạo tiến trình → giữ chỗ (§4.4) → tạo thanh toán → `POST /api/billing/patient/payments/{paymentIntentId}/checkout` (Billing) lấy liên kết VNPAY → sau IPN, worker xác nhận lịch → xem lịch hẹn.

`POST /api/scheduling/patient/booking-attempts`

- Header `Idempotency-Key` bắt buộc. Body JSON `{ "patientId": "<uuid>", "serviceDate": "yyyy-MM-dd" }`.
- 201 `{ "bookingAttemptId" }`, header `Location: /api/scheduling/patient/booking-attempts/{bookingAttemptId}`. Gửi lại cùng khóa và nội dung trả cùng `bookingAttemptId`.
- Lỗi: 400 `InvalidInput` · 415 `UnsupportedMediaType` · 404 `PatientNotFound` (hồ sơ không tồn tại hoặc người gọi không có quyền Booking, cùng một mã) · 409 `IdempotencyConflict`, `RequestInProgress`.

`POST /api/scheduling/patient/booking-attempts/{bookingAttemptId:guid}/payment`

- Không body, không `Idempotency-Key`: khóa chống lặp là lần giữ chỗ hiện tại (ReservationId) phía server. Mỗi lần giữ chỗ chỉ có một khoản thu phí khám và một yêu cầu thanh toán; gọi lại trả đúng yêu cầu đã tạo.
- 200 `{ "paymentIntentId", "amount", "currency", "expiresAtUtc" }`. `expiresAtUtc` bằng hạn giữ chỗ. Phí áp tại thời điểm giữ chỗ, do Billing tra bảng giá; không nhận số tiền từ client.
- Khoản thu, yêu cầu thanh toán của Billing và trạng thái tiến trình (`WaitingPayment`, `PaymentInitiated`) commit cùng một transaction (host đăng ký `BillingDbContext` làm participant của unit of work).
- Route không trả liên kết VNPAY: client gọi tiếp route checkout của Billing với `paymentIntentId`.
- Lỗi: 404 `AttemptNotFound` (không có, không thuộc người gọi, mất quyền Booking, hoặc chưa giữ chỗ lần nào), `DoctorNotFound` · 409 `HoldExpired` (giữ chỗ không còn `Held` hoặc `expiresAtUtc <= giờ DB`), `InvalidAttemptState`, `RequestInProgress`, và mã của Billing `PriceNotConfigured`, `AmbiguousPrice`, `PaymentConflict`, `ChargeNotPayable`.

`GET /api/scheduling/patient/appointments?page=1&pageSize=20`

- Lịch hẹn do chính người gọi đặt (`Reservation.AppUserId`), ngày khám mới nhất trước. Lịch tiếp tân đặt hộ tại quầy chưa hiện (bổ sung khi Identity có API liệt kê hồ sơ được đại diện). Lịch của hồ sơ mà người gọi đã mất quyền Booking bị loại khỏi trang.
- 200 `{ "page", "pageSize", "hasMore", "items": [ { "appointmentId", "appointmentNumber", "status", "patientId", "doctorId", "doctorName" (nullable), "serviceDate", "assignedRoomId", "appliedConsultationFee", "currency", "confirmedAtUtc", "checkInDeadlineUtc" (nullable), "cancelledAtUtc" (nullable) } ] }`. `pageSize` tối đa 50.
- Lỗi: 400 `InvalidInput`.

`GET /api/scheduling/patient/appointments/{appointmentId:guid}`

- 200: một phần tử như trên. Số thứ tự hàng đợi chưa có trong phản hồi (cấp lúc check in, W3-02).
- Lỗi: 404 `AppointmentNotFound` (không có, không do người gọi đặt, hoặc mất quyền Booking).

### 4.6 Check-in tại quầy, lịch hôm nay, hàng chờ phòng, màn hình chờ (W3-02, W4-02 REST; 17/11/2026)

`POST /api/scheduling/staff/appointments/{appointmentId:guid}/check-in` — `ReceptionistOnly` (kênh Staff)

- Không body, không `Idempotency-Key`; idempotent tự nhiên theo `AppointmentId`. Người thực hiện lấy từ token. Tiếp tân đối chiếu giấy tờ ngoài hệ thống; API không nhận CCCD.
- 200 `CheckInResult` (§2.2). 400 `InvalidInput`; 404 `AppointmentNotFound`; 409 `InvalidAppointmentState` | `CheckInNotOpen` | `CheckInDeadlinePassed`. Lỗi trả ProblemDetails có `code`.

`GET /api/scheduling/staff/appointments/today?page=1&pageSize=20&doctorId=&appointmentNumber=` — `ReceptionistOnly`

- Ngày Việt Nam lấy từ giờ DB. `page` 1..1000, `pageSize` 1..50, `doctorId` GUID khác rỗng, `appointmentNumber` đối sánh chính xác, tối đa 64 ký tự; sai → 400 `InvalidInput`.
- 200 `{ page, pageSize, hasMore, items }`; item `{ appointmentId, appointmentNumber, patientId, doctorId, assignedRoomId, serviceDate, status, checkInDeadlineUtc }`. Không tên, điện thoại, CCCD hay bệnh án.

`GET /api/scheduling/staff/rooms/{roomId:guid}/queue?page=1&pageSize=50` — kênh Staff, vai trò Receptionist, Nurse, Doctor hoặc RoomAccount

- RoomAccount chỉ xem đúng `RoomId` trong token; Doctor chỉ xem phòng mình được phân công hôm nay; sai → 403. `page` 1..1000, `pageSize` 1..100; sai → 400 `InvalidInput`.
- 200 `{ serviceDate, page, pageSize, hasMore, items }`; item `{ queueTicketId, ticketNumber, sequenceNumber, ticketType, queueStatus, targetStationId?, calledAtUtc? }`, chỉ vé `Waiting`, `Called`, `Deferred`, `InProgress`, theo thứ tự phát vé.

`GET /api/scheduling/public/display?roomId=` — ẩn danh (màn hình chờ)

- 200 `{ tickets: [{ ticketNumber, sequenceNumber }] }`: vé đang `Called`/`InProgress` của phòng, mới nhất trước. Chỉ số vé, không PII. `roomId` thiếu hoặc sai → 400 `InvalidInput`.

### 4.6a Tra hồ sơ và đăng ký tại quầy (W3-01; v0.9, 17/11/2026)

`GET /api/scheduling/staff/reception/search?keyword=` — `ReceptionistOnly`

- `keyword`: số điện thoại hoặc CCCD, bỏ khoảng trắng còn 9–20 ký tự; sai → 400 `InvalidInput`. Khớp chính xác (Identity §10), tối đa 20 hồ sơ.
- 200 `[{ patientId, fullName, dateOfBirth, gender, maskedPhoneNumber, maskedNationalId?, isVerified }]`; không thấy → `[]`. Chưa kèm lịch hẹn hôm nay: dùng `GET /staff/appointments/today` (§4.6).

`POST /api/scheduling/staff/walk-in` — `ReceptionistOnly`, header `Idempotency-Key` (≤ 64 ký tự)

- Body `{ patientId, doctorId }` (PatientId lấy từ tra cứu ở trên). Hôm nay theo giờ DB; giữ chỗ dùng chung quota 30 suất với đặt online, thu tiền mặt phí khám (Billing `Counter`), xác nhận và check-in ngay.
- 201 `{ result: "Registered", bookingAttemptId, appointmentNumber, checkIn, receipt }`. 400 `InvalidInput`; 404 `PatientNotFound` | `DoctorNotFound` | `DayNotFound`; 409 mã giữ chỗ (`Full`, `DayClosed`, `CutoffPassed`, `AlreadyClaimed`), `InProgress`, `IdempotencyConflict`. Gửi lặp cùng khóa trả kết quả cũ.

### 4.7 Hủy, đổi lịch và tiến trình dang dở phía bệnh nhân (W2-07, W2-04; v0.6, 17/11/2026)

Chung: policy `PatientOnly`. Lịch không có, không do người gọi đặt, hoặc người gọi mất quyền Booking trên hồ sơ đều trả cùng 404 `AppointmentNotFound`. Header `Idempotency-Key` bắt buộc (một giá trị); gửi lại cùng khóa và nội dung trả kết quả đã lưu, khác nội dung trả 409 `IdempotencyConflict`; lệnh cùng khóa đang chạy trả 202 `{ operationId }`. Body phải là JSON (415 `UnsupportedMediaType`). Mốc 05:00 ngày D so bằng giờ DB sau khi nhận khóa. Lỗi nghiệp vụ là ProblemDetails có `code` = tên `ChangeResult`.

`POST /api/scheduling/patient/appointments/{appointmentId:guid}/cancel`

- Body tùy chọn `{ "reason": "<≤ 500 ký tự>" }` (có thể bỏ trống body). Chỉ lịch `Scheduled`.
- 200 `{ appointmentId, status: "Cancelled", refund: "Full" | "None", refundAmount, currency, cancelledAtUtc, operationId }`. `Full` khi hủy trước 05:00 ngày D: trả suất và Billing tạo yêu cầu hoàn qua `AppointmentCancelled`; từ 05:00: `None`, khóa suất.
- Lỗi: 400 `InvalidInput` · 404 `AppointmentNotFound` · 409 `InvalidAppointmentState`, `IdempotencyConflict`, `RequestInProgress`.

`POST /api/scheduling/patient/appointments/{appointmentId:guid}/reschedule`

- Body `{ "targetDoctorId": "<uuid>", "targetServiceDate": "yyyy-MM-dd" }`. Hạng bác sĩ đích lấy phía server; mức phí so theo bảng giá đang hiệu lực.
- 200: lịch hẹn mới, cùng dạng phần tử của `GET /patient/appointments` (§4.5); lịch cũ chuyển `Rescheduled`.
- Lỗi: 400 `InvalidInput` · 404 `AppointmentNotFound`, `DoctorNotFound` (bác sĩ đích không có hoặc ngừng), `DayNotFound` · 409 `RescheduleCutoffPassed`, `DifferentFee`, `SameDay`, `DayClosed`, `CutoffPassed`, `Full`, `AlreadyClaimed`, `InvalidAppointmentState`, `VersionConflict`, `IdempotencyConflict`, `RequestInProgress`.

`GET /api/scheduling/patient/booking-attempts/recoverable`

- Tiến trình đặt lịch của người gọi ở trạng thái `Draft`, `Held`, `WaitingPayment`, `Confirming`, `NeedsReselection`, ngày khám từ hôm nay (giờ Việt Nam theo DB), mới cập nhật trước, tối đa 20; bỏ hồ sơ người gọi đã mất quyền Booking.
- 200 mảng `{ bookingAttemptId, patientId, serviceDate, doctorId?, status, currentStep, expiresAtUtc?, allowedActions }`; `status`/`allowedActions` tính như `GET /patient/booking-attempts/{id}` (§4.4).

Chưa có (ngoài v0.6): `GET .../cancellation-preview`, `GET .../reschedule-options`, `GET .../ticket`.

### 4.8 Hàng đợi phòng: gọi người tiếp theo, bắt đầu, khám xong, trả về hàng chờ (W3-03, W3-08; v0.7, 17/11/2026)

Chung: kênh **Staff**, vai trò `Doctor`, `Nurse` hoặc `RoomAccount` (tiếp tân, admin, bệnh nhân → 403). Phạm vi phòng (W3-08): `RoomAccount` chỉ thao tác phòng có `room_id` trong token; `Doctor` chỉ phòng của phiên mở hôm nay (giờ Việt Nam); `Nurse` không giới hạn phòng. Sai phòng → 403. Một phiên mỗi phòng do Identity bảo đảm (đăng nhập mới thu hồi phiên cũ). Lỗi: ProblemDetails có `code` = `QueueResult`. Mỗi thay đổi trạng thái vé ghi AuditLog.

Phản hồi vé: `{ queueTicketId, ticketNumber, sequenceNumber, ticketType, queueStatus, targetRoomId, stationId?, calledAtUtc?, alreadyCalled }`.

`POST /api/scheduling/staff/rooms/{roomId:guid}/call-next`

- Body tùy chọn `{ "stationId": "<uuid>" }`. Không gửi: dùng station đang hoạt động duy nhất của phòng; phòng chưa có station nào thì Scheduling tạo station mặc định `DEFAULT-<roomId:N>` (chốt 17/11/2026); phòng có nhiều station → 409 `StationRequired`.
- Chọn vé `Waiting` của phòng vào hàng từ đầu ngày (`QueuedAtUtc`, giờ Việt Nam): vé quay lại phát hôm trước, kích hoạt hôm nay vẫn có. Hai nhóm: mới (khám đầu, CLS; theo `SequenceNumber`) và quay lại (theo `QueuedAtUtc`). Cả hai có người: 2 mới rồi 1 quay lại; đầu nhóm nào chờ ≥ 20 phút thì nhóm chờ lâu hơn được trước (chống bỏ đói).
- Với từng ứng viên: transaction ngắn, khóa bệnh nhân rồi station (`sp_getapplock`, cùng thứ tự ở mọi lệnh), kiểm lại station trống, bệnh nhân chưa ở điểm khác, vé còn `Waiting`; cấp `ActivePatientEngagement` + `ActiveStationAllocation`, vé → `Called`. Bệnh nhân đang ở điểm khác: bỏ qua, vé giữ `Waiting` và số gốc.
- 200 vé được gọi. Station đã có người (bấm hai lần, hai người cùng bấm): 200 vé đang gọi đó với `alreadyCalled = true`, không cấp vé thứ hai. 204 không còn ai. Lỗi: 400 `InvalidInput` · 404 `StationNotFound` (phòng không hoạt động, station không thuộc phòng hoặc đã tắt) · 409 `StationRequired`.

`POST /api/scheduling/staff/tickets/{ticketId:guid}/start` · `/finish` · `/requeue`

- `start`: `Called` → `InProgress` (`ServedAtUtc`). `finish`: `InProgress` → `Finished` (`CompletedAtUtc`), giải phóng bệnh nhân và station. `requeue` (gọi không có mặt): `Called` → `Waiting`, bỏ station và `CalledAtUtc`, giữ số gốc, giải phóng bệnh nhân và station.
- Gửi lại khi vé đã ở trạng thái đích: 200 với `alreadyCalled = true`. Lỗi: 403 (sai phòng) · 404 `TicketNotFound` · 409 `InvalidTicketState`.
- `InProgress` mất kết nối chỉ cảnh báo ở UI; không có job tự giải phóng (Giải pháp §6.2). Phòng CLS chỉ bắt đầu khi khoản phí đã xác minh: kiểm ở W3-05, chưa có trong v0.7.

DB (migration `QueueTicketActivationAndSequence`, 17/11/2026): cột `ActivatedAtUtc`, cột tính `QueuedAtUtc`, index phủ `(TargetRoomId, QueuedAtUtc)`, bảng đếm `TicketSequence`. DB (migration `AddQueueTicketUniqueIndexes`): unique `TicketNumber`; unique `ServiceOrderItemId` khi khác null; một vé khám đầu mỗi `EncounterId`; một vé quay lại mỗi `ServiceOrderId`.

## 5. HTTP dự kiến của W2 — chưa chốt

Chỉ route và mục đích, để UI (W2-09) mock trước. Body, mã lỗi và quyền chốt trong bản contract sau. Route có tiền tố kênh `/patient/`, `/staff/`, `/admin/` (Q7, đã chốt 09/11/2026); Backend kiểm vai trò theo kênh. Mọi lệnh thay đổi nhận `Idempotency-Key`. Danh sách trả `items, page, pageSize, hasMore` (Q6, đã chốt).

| Method + route | Mục đích | Task |
|---|---|---|
| `POST /api/scheduling/patient/booking-attempts` | Đã có, chi tiết §4.5 | W2-03 |
| `GET /api/scheduling/patient/booking-attempts/{bookingAttemptId}` | Khôi phục tiến trình: trạng thái, bước đã xong, `expiresAtUtc`, `allowedActions` (Giải pháp §2.3); chi tiết §4.4 | W2-04 |
| `POST /api/scheduling/patient/booking-attempts/{bookingAttemptId}/hold` | Giữ chỗ bác sĩ + ngày (5 phút); chi tiết §4.4 | W2-02, W2-04 |
| `POST /api/scheduling/patient/booking-attempts/{bookingAttemptId}/payment` | Đã có, chi tiết §4.5 (trả `paymentIntentId`, liên kết VNPAY lấy ở route checkout của Billing) | W2-02/W2-06 |
| `GET /api/scheduling/patient/operations/{operationId}` | Tra lệnh dài (202 + `operationId`); chi tiết §4.4 | W2-03, W2-04 |
| `GET /api/scheduling/patient/appointments` | Đã có, chi tiết §4.5 | W2-07 |
| `GET /api/scheduling/patient/appointments/{appointmentId}` | Đã có, chi tiết §4.5; số thứ tự bổ sung ở W3-02 | W2-07 |
| ~~`POST /api/scheduling/patient/appointments/{appointmentId}/reschedule`~~ (đã có, §4.7) | Đổi lịch trước 05:00 sang bác sĩ cùng mức phí | W2-07 |
| `GET /api/scheduling/patient/appointments/{appointmentId}/cancellation-preview` | Số tiền được hoàn / không hoàn trước khi xác nhận hủy | W2-07 |
| ~~`POST /api/scheduling/patient/appointments/{appointmentId}/cancel`~~ (đã có, §4.7) | Bệnh nhân hủy lịch (chỉ Scheduled) | W2-07 |
| `GET /api/scheduling/staff/doctors` | Tiếp tân tìm bác sĩ, suất trống, phí; cùng `DoctorSearchService` và DTO với route patient, policy nhân viên quầy (vai trò `Receptionist`) (Q2, đề xuất, chờ trưởng nhóm duyệt) | W3-01 |
| ~~`POST /api/scheduling/staff/appointments/{appointmentId}/check-in`~~ | Đã có, xem §4.6 | W3-02 |
| `POST /api/scheduling/staff/appointments/{appointmentId}/cancel` | Tiếp tân hủy (Scheduled; CheckedIn bắt buộc lý do) | W2-07 |

## 6. Câu hỏi mở

| # | Câu hỏi | Ảnh hưởng tới contract | Trạng thái |
|---|---|---|---|
| Q1 | Vé khám đầu có trước hay sau Encounter? | Check-in gọi `IClinicalModule.StartEncounterAsync` đồng bộ trong cùng `IUnitOfWork`, lấy `EncounterId` tạo `QueueTicket` khám đầu; `TicketCheckedIn` mang `EncounterId`; Clinical không có handler. `CheckInResult` thêm `EncounterId`, bỏ nullable trường vé (§2.2, §3). | **Đã chốt 17/11/2026** (trưởng nhóm) |
| Q2 | API tìm bác sĩ có bản Staff cho tiếp tân không? | Đề xuất: route riêng `GET /api/scheduling/staff/doctors`, cùng `DoctorSearchService`, policy nhân viên quầy (§5). | Đề xuất, chờ trưởng nhóm duyệt |
| Q3 | Bệnh viện hủy sau 05:00 (bác sĩ nghỉ): trả suất hay khóa suất? | Đề xuất: mọi giờ đều trả suất (`SlotReleased = true`, `AdmittedCount − 1`, không tăng `LockedCancelledCount`), `Refund = Full` (§3). | Đề xuất, chờ trưởng nhóm duyệt |
| Q4 | Có sự kiện riêng khi đổi lịch không? | Đề xuất: không trong MVP; Notifications dùng `AppointmentConfirmed` có `RescheduledFromAppointmentId` (§3). | Đề xuất, chờ trưởng nhóm duyệt |
| Q5 | Giới hạn số ngày tới được mở lịch? | Đề xuất: không có cấu hình số ngày; chỉ ngày `IsOpen` và `serviceDate` ≥ hôm nay theo giờ DB (ICT) (§1). | Đề xuất, chờ trưởng nhóm duyệt |
| Q6 | Phân trang `hasMore` hay `totalCount`? | `items, page, pageSize, hasMore`, không `totalCount`; docs/conventions.md §3 (PR #43) đã sửa theo. | **Đã chốt 09/11/2026** |
| Q7 | Tiền tố kênh trong route? | `/api/<module>/patient/...`, `/staff/...`, `/admin/...`; Backend kiểm vai trò theo kênh; §5 đã đổi theo. | **Đã chốt 09/11/2026** |

## 7. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 | 09/11/2026 | Bản đầu (W1-04): `ISchedulingModule` (tìm bác sĩ theo DTO của PR #44, check in, vé CLS + vé quay lại), 3 sự kiện, HTTP W2-01, W2-05 theo PR #44, danh sách endpoint W2 dự kiến |
| v0.1 (sửa) | 09/11/2026 | Q6 (hasMore), Q7 (tiền tố kênh) đã chốt; route §5 thêm tiền tố kênh và route staff; đề xuất Q1–Q5 chờ duyệt; Q3 ghi vào `AppointmentCancelled` |
| v0.1 Approved | 10/11/2026 | H-02 duyệt; §4.1–4.3 đối chiếu `dev` sau FIX-013 (policy vai trò + kênh) và W2-05 phần 1 (đã merge, `PatientOnly`, bỏ 503 không còn trong code); Phạm vi v2.12, Giải pháp v2.14 |
| v0.2 | 10/11/2026 | Thêm §4.4 (W2-04: khôi phục tiến trình, tra Operation, giữ chỗ qua web) theo đề nghị OWN-14 của PR #55; thêm mã 415 `UnsupportedMediaType` |
| v0.2 Approved | 10/11/2026 | Trưởng nhóm duyệt §4.4; `allowedActions` theo PR #55 đã merge (`NeedsReselection` đã lưu → `["Hold"]`, hết hạn tính lúc đọc → `[]`) |
| v0.3 | 12/11/2026 | Thêm §4.5: tạo tiến trình đặt lịch, tạo thanh toán phí khám sau giữ chỗ, danh sách và chi tiết lịch hẹn của bệnh nhân; §5 đánh dấu 4 route đã có |
| v0.3 Approved | 12/11/2026 | Trưởng nhóm duyệt §4.5 |
| v0.4 | 12/11/2026 | `DoctorSearchItem` thêm `TotalSlots`, `DoctorRankName`, `SpecialtyName` (chỉ thêm trường, không đổi trường cũ) để web bệnh nhân hiện "còn x/y suất" và tên chuyên khoa, cấp bậc |
| v0.4 Approved | 12/11/2026 | Trưởng nhóm duyệt phần thêm của v0.4 |
| v0.5 | 17/11/2026 | Q1 đã chốt (Encounter mở đồng bộ cùng UoW); `CheckInResult` và `TicketCheckedIn` có `EncounterId`, trường vé không nullable; thêm §4.6 (check-in, lịch hôm nay, hàng chờ phòng, màn hình chờ) |
| v0.5 (sửa) | 17/11/2026 | §4.1: mở ngày trực nhận body `roomId` (admin chọn phòng), thêm 404 `RoomNotFound`, thêm `GET /admin/doctor-days?serviceDate=`; bỏ cổng Development của Scheduling, danh bạ bác sĩ thật qua Identity |
| v0.6 | 17/11/2026 | Thêm §4.7: bệnh nhân hủy, đổi lịch (Idempotency-Key) và `GET /patient/booking-attempts/recoverable` |
| v0.7 | 17/11/2026 | §2.3 hiện thực vé CLS + vé quay lại (Deferred đến khi kích hoạt); thêm §4.8 hàng đợi phòng (call-next, start, finish, requeue) và policy tài khoản phòng |
| v0.8 | 17/11/2026 | Vé quay lại kích hoạt ghi `ActivatedAtUtc` (giữ `IssuedAtUtc`), hàng chờ theo `QueuedAtUtc`; số vé từ bảng đếm `TicketSequence` (CLA-PERF-001, không đổi route hay body) |
| v0.9 | 17/11/2026 | Thêm §4.6a: tiếp tân tra hồ sơ theo SĐT/CCCD, route đăng ký tại quầy được nối vào Api (trước đó chưa map) |
| v0.10 | 17/11/2026 | §4.1 mở ngày trực: `roomId` tùy chọn, thiếu thì lấy phòng theo ca trực; thêm 400 `RoomRequired` |
| v0.11 | 18/11/2026 | §3 thêm sự kiện `QueueCalled`, `ClsTicketCancelled`; job cuối ngày CLS, kết thúc phiên; `ICoordinationExceptionReader` |
