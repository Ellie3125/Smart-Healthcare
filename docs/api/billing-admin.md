# Billing quản trị và chốt ca — schema gate

- contract_version: v0.1 Draft,18/11/2026.
- Chưa map endpoint mới. Chỉ tài liệu thiếu hụt theo chỉ đạo dừng việc khi thiếu schema.

| Route web gọi | Trạng thái |
|---|---|
| GET/POST `/api/billing/admin/price-lists` | PriceList/PriceListItem có schema; chưa triển khai do việc3 dừng; cần tách phần độc lập nếu muốn làm trước |
| GET `/api/billing/admin/cls-waivers` | Chờ schema hồ sơ yêu cầu miễn CLS |
| POST `/api/billing/admin/cls-waivers/{id}/decision` | Chờ schema lưu quyết định một lần và đồng bộ nghĩa vụ thu |
| POST `/api/billing/staff/cashier/close-shift` | Chờ schema chốt ca bền và unique cashier/shift |

## Cần schema

`BillingItemStatus.Waived` đã có, nhưng không có hồ sơ yêu cầu miễn/duyệt/từ chối chứa lý do,số tiền,actor,thời điểm và concurrency token. Không thể cung cấp danh sách yêu cầu hoặc bảo đảm quyết định một lần từ một flag cuối. Owner cần thiết kế hồ sơ gắn BillingItem/ServiceOrderItem và quy tắc miễn nghĩa vụ; không tự thay Status hay TotalAmount của khoản đã thu.

Chưa có bản ghi chốt ca: định danh cashier/shift,start/end,cash collected/refunded snapshot,actualCashCounted,discrepancyNote,closedBy/At và unique cashier+shift. Web gửi Idempotency-Key; record idempotency không ngăn chốt lại bằng key khác,phải có guard business bền. `CashierService.GetTodaySummaryAsync`/GET `/api/billing/staff/cashier/shift-summary` đang có chỉ tổng hợp trong ngày,không thể biến thành bằng chứng ca đã chốt.

## Phần bảng giá đang có
`IPriceCatalog`/`PriceResolver` lấy giá theo EffectiveFromUtc≤t<EffectiveToUtc,ưu tiên bác sĩ riêng rồi cấp bậc,nhiều giá cùng căn cứ báo AmbiguousPrice. `BillingItem` giữ UnitPrice/TotalAmount snapshot. Thiết kế admin sau khi được giao tiếp phải giữ các quy tắc này,chốt cách thay thế lịch giá không chồng; không đổi khoản đã thu. Frontend hiện gửi rankName,cần ánh xạ DoctorRankId và chốt date GMT+7.

Hai nhóm admin dự kiến AdminOnly/Staff,chốt ca ReceptionistOnly/Staff. Auth/error/idempotency contract chi tiết chỉ chốt khi có schema; chưa có endpoint để xác nhận401/403/409. Không tạo migration/seed/route giả; không sửa VNPAY,tạoURL,IPN,querydr,PaymentIntent hoặc luồng thu/hoàn. Không cần sửa lõi trong PR tài liệu này.
