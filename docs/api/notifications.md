# Contract module Notifications (workflow)

- contract_version: **v0.3**
- Trạng thái: **Draft**, chờ trưởng nhóm duyệt (H-02).
- Người giữ: Claude Code (ownership.md mục 1). Task: W2-03.
- Mã nguồn: `backend/src/Modules/Notifications/Notifications.Contracts/IOperationTracker.cs`.
- Nguồn: Giải pháp v2.14 §2.2, §2.3, §4; ERD v2.4 `workflow.Operation`.

## 1. `IOperationTracker` (trong tiến trình)

| Method | Ý nghĩa | Ràng buộc |
|---|---|---|
| `StartAsync(StartOperation)` | Tạo Operation trạng thái `Running`, `CreatedAtUtc` = giờ DB | Gọi trong `IUnitOfWork`, cùng transaction với tác dụng nghiệp vụ |
| `CompleteAsync(operationId, status, failureReason)` | Đặt trạng thái cuối, `CompletedAtUtc` = giờ DB | Gọi trong `IUnitOfWork` |
| `GetAsync(operationId)` | Đọc `OperationView`, `null` nếu không có | Không cần transaction. Bên gọi kiểm quyền (`InitiatedByUserId`) trước khi trả ra HTTP (W2-04) |

Enum `OperationType`, `OperationStatus`, `TargetEntityType` trùng tên giá trị với ERD v2.4; `ArchitectureTests.OperationEnumParityTests` giữ hai bên khớp.

## 2. Cách Scheduling dùng (W2-03)

- Giữ chỗ là lệnh đồng bộ: `IIdempotencyStore.BeginAsync` (phạm vi `AppUser:{id}`, loại `Scheduling.HoldSlot`, giữ 24 giờ) → `StartAsync` → giữ chỗ → `CompleteAsync` (`Succeeded` khi giữ được; `NeedsAction` kèm `failureReason` = mã kết quả khi bị từ chối) → `IIdempotencyStore.CompleteAsync` với mã HTTP và body. Tất cả commit một lần.
- Gửi lặp cùng khóa và cùng nội dung trả lại đúng kết quả cũ, cùng `OperationId`, kể cả kết quả từ chối. Cùng khóa khác nội dung: `IdempotencyConflict`. Lỗi trước commit không để lại Operation hay bản ghi idempotency.
- Tạo BookingAttempt (`Scheduling.CreateBookingAttempt`) chỉ dùng idempotency, không tạo Operation (ERD không có loại tương ứng).

## 3. Thời gian thực: hub SignalR và `IRealtimeNotifier` (W4-01, v0.2)

Mã nguồn: `Notifications.Contracts/IRealtimeNotifier.cs`, `Notifications.Api/RealtimeHubs.cs`. Client chỉ dùng WebSockets với `skipNegotiation: true` (không sticky session); token gửi qua query `access_token`, Backend chỉ đọc query này ở đường dẫn `/hubs`. Client nhận sự kiện rồi tải lại dữ liệu qua REST; mất kết nối thì tự kết nối lại và tải lại.

| Hub | Ai kết nối | Nhóm | Sự kiện client đang nghe |
|---|---|---|---|
| `/hubs/staff` | Kênh Staff, vai trò Admin, Receptionist, Nurse, Doctor, RoomAccount (vai trò khác 401/403 trước khi nâng cấp WebSocket) | theo AppUser; tài khoản phòng thêm nhóm theo `room_id` | `NotificationReceived`, `QueueUpdated`, `SessionReplaced` |
| `/hubs/patient` | `PatientOnly` | theo AppUser | `JourneyUpdated`, `QueueCalled` |
| `/hubs/public-display?roomId=` | Ẩn danh (màn hình gọi số) | theo phòng; không có `roomId` thì nhóm mọi phòng | `DisplayBoardUpdated` (chỉ số vé, không PII) |

Nginx chặn `/hubs/staff` ở host công khai và `/hubs/patient`, `/hubs/public-display` ở host nhân viên.

`IRealtimeNotifier` (module khác gọi, ví dụ W4-02, W4-03): `ToStaffUserAsync`, `ToStaffRoomAsync`, `ToPatientUserAsync`, `ToPublicDisplayAsync(roomId, eventName, payload)`. Tên sự kiện ở `RealtimeEvents`. Gửi kiểu cố gắng, tối đa 3 giây: lỗi (Redis chết, không có kết nối) chỉ ghi log cảnh báo, không ném lỗi, nên gọi sau khi nghiệp vụ đã commit (thường trong handler Outbox ở Worker).

Backplane: `ConnectionStrings:Redis` có giá trị thì Api (BE1, BE2) và Worker dùng Redis (`ChannelPrefix = SmartHealthcare`, `AbortOnConnectFail = false`: Redis chết Api vẫn khởi động, đặt lịch vẫn chạy); trống thì một instance không backplane (máy dev). Package `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 10.0.15 (bản vá GHSA-wr8v-9x5q-ggcm).

## 3a. Thông báo trên web bệnh nhân (W4-03, v0.3, 17/11/2026)

Lưu bền trong `workflow.PatientNotification` (một dòng mỗi sự kiện × hồ sơ × loại; unique `(SourceEventId, PatientId, Kind)` nên xử lý lặp không tạo trùng) và `workflow.PatientNotificationRead` (đã đọc riêng từng tài khoản, PK `(NotificationId, AppUserId)`). Không lưu bệnh án, tên, điện thoại hay nội dung tự do: tiêu đề và nội dung sinh từ mẫu cố định theo `Kind`. Không push trong MVP.

`GET /api/notifications/patient/notifications?patientId=&unreadOnly=false&page=1&pageSize=50` — `PatientOnly`

- `patientId` bắt buộc (hồ sơ đang chọn); `pageSize` 1..100. Kiểm `CanAccessPatientAsync(user, patientId, Booking)` trước khi đọc. Thông báo `ResultsAvailable` chỉ hiện khi người gọi có quyền `ClinicalRecords` và bác sĩ đã phát hành kết luận sau CLS.
- 200 `{ items, unreadCount, hasMore }`; item `{ id, title, body, category: Appointment|Clinical|Queue, status: Sent|Read, createdAt, targetPath: null }`. `unreadCount` theo tài khoản đang đăng nhập.
- 400 `InvalidInput`; 404 khi không có quyền hồ sơ (không phân biệt với hồ sơ không tồn tại); 401/403.

`POST /api/notifications/patient/notifications/{id}/read` — `PatientOnly`

- Không body. 204; gọi lặp vẫn 204. 404 khi không có hoặc không còn quyền xem hồ sơ của thông báo.

`Kind`: AppointmentConfirmed, AppointmentCancelled, DoctorChanged, ClsOrdered, ResultsAvailable, TicketCalled, ReturnTicketActivated. Đã nối handler cho AppointmentConfirmed, AppointmentCancelled; các loại khác có mẫu, nối khi luồng phát sự kiện tương ứng xong.

## 4. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 | 09/11/2026 | Bản đầu (W2-03) |
| v0.2 | 17/11/2026 | §3 hub SignalR, `IRealtimeNotifier`, Redis backplane (W4-01) |
| v0.3 | 17/11/2026 | §3a thông báo bệnh nhân lưu bền + đã đọc theo tài khoản (W4-03), migration Workflow `PatientNotifications` |
