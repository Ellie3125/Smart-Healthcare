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

## 3. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 | 09/11/2026 | Bản đầu (W2-03) |
