# api-contract-and-handoff

## Phạm vi
Codex sở hữu docs/api; hai agent dùng contract để tích hợp.

## Khi nào đọc
Trước UI/API mới hoặc thay schema/trạng thái.

## Quy tắc
- **API-01** — Contract có version/status Draft hoặc Approved, endpoint/method, auth+object permission, request/response, nullability/enum, timestamp/múi giờ/tiền, pagination/filter/sort, lỗi/status code, idempotency và ví dụ giả lập.
- **API-02** — Codex đề xuất contract; Antigravity phản hồi trong handoff riêng; chốt bản Approved trước tích hợp. Breaking change có migration plan, version, consumer impact và acceptance criteria; không đổi ngầm.
- **API-03** — Handoff ghi task/use case, contract commit/hash, trạng thái BE/UI-mock/UI-real/E2E, kết quả đã chạy, thiếu sót, next_action. Handoff không phải quyền sửa bên còn lại.
- **API-04** — Ngày giờ API dùng ISO-8601 với timezone xác định, lưu UTC và hiển thị Asia/Ho_Chi_Minh theo contract; tiền VND và quy đổi VNPAY được xác định server-side. Không làm tròn ngầm.

## Điều cấm
Không tạo endpoint guessed hay đánh dấu contract mẫu là Approved. UI mock pass không là integration pass.

## Cách kiểm chứng
OpenAPI validate và contract diff khi có schema; đối chiếu mock với contract; BE test lỗi và object permission; frontend compile theo contract; E2E API thật trước feature done.

## Nguồn
Master IV §5–6,10 và I §8. Tạo contract thật tại docs/api/ theo các trường bắt buộc trong API-01; không phụ thuộc file mẫu.
