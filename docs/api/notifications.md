# Contract module Notifications (workflow)

- contract_version: **v0.1**
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

## 4. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 | 09/11/2026 | Bản đầu (W2-03) |
| v0.2 | 17/11/2026 | §3 hub SignalR, `IRealtimeNotifier`, Redis backplane (W4-01) |
