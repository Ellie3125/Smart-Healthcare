# Billing — chốt ngày thu ngân

Contract v0.1 Draft, 18/11/2026. Dùng schema `CashierShiftClosure` đã có từ PR #163.

Các route dưới đây dùng `ReceptionistOnly`, kênh Staff như nhóm cashier hiện có: 401 nếu ẩn danh hoặc thiếu actor Guid hợp lệ, 403 nếu sai role/kênh. Actor lấy từ phiên; body không nhận cashier/date.

## POST `/api/billing/staff/cashier/close-shift`

Body JSON: `{ "actualCashCounted": 100000, "discrepancyNote": "" }`. Amount bắt buộc, từ 0 đến 9999999999999999.99, tối đa hai chữ số thập phân; không làm tròn. Note nullable, trim, tối đa 500 ký tự. Tiền đếm khác thu trừ hoàn thì phải có note. Vi phạm trả 400 `InvalidInput`; JSON null/sai/thiếu amount trả 400; không dùng JSON trả 415 `UnsupportedMediaType`.

Một header `Idempotency-Key` bắt buộc, 1–128 ký tự ASCII hiển thị, không có khoảng trắng; TTL 24 giờ, theo AppUser + `Billing.CloseCashierShift`. Cùng key và payload chuẩn hóa (amount hai chữ số, note trim/null) trả 200 với response đã lưu, kể cả sau chốt. Khác payload trả 409 `IdempotencyConflict`; request đang chạy trả 409 `RequestInProgress`. Key khác chốt lại cùng ngày trả 409 `CashierShiftClosed`. Key không scoped theo ngày: client dùng key mới cho lần chốt ngày mới; retry key cũ trong TTL có thể trả biên bản ngày cũ.

Ca là ngày GMT+7 của tài khoản tiếp tân, lấy thời gian từ `IDbClock`. Trong cùng `IUnitOfWork` Shared + Billing: idempotency → khóa cashier/date (`UPDLOCK,HOLDLOCK`, kể cả chưa có row) → gọi **CashierService.GetTodaySummaryAsync** → validate → insert closure → BillingAuditWriter → lưu response idempotency → commit. Bản chụp số phiếu, số hoàn, tổng thu/hoàn, tiền đếm, note và ClosedAtUtc bất biến. Lỗi/audit failure rollback toàn bộ. Vi phạm unique DB 2601/2627 cũng trả 409 `CashierShiftClosed`.

Response 200 giữ các field `CashierShiftSummary` cũ và bổ sung đủ field của `ShiftReconcileSummary`:
`shiftDate,cashierUserId,cashierName,receiptCount,cashCollected,cashRefunded,netCash,currency,receipts,refunds,isClosed,closedAt,actualCashCounted,discrepancyNote,expectedCashCollected,expectedCashRefunded,netExpectedCash`.

Các alias expectedCashCollected, expectedCashRefunded, netExpectedCash lần lượt bằng cashCollected, cashRefunded, netCash. Ngày có dạng yyyy-MM-dd, closedAt là UTC ISO8601, tiền là JSON number. Tên lấy qua `GetActiveStaffAsync`; thiếu tên trả chuỗi rỗng. Cách tính tổng thu/hoàn của `GetTodaySummaryAsync` được giữ nguyên.

## GET `/api/billing/staff/cashier/shift-summary`

Route cũ trả response trên. Chưa chốt: `isClosed=false`, closedAt/actual/note null. Đã chốt: amounts và receiptCount lấy bản chụp; receipts/refunds vẫn từ nguồn cũ để đối chiếu. Không sửa hoặc xóa biên bản.

## Kiểm tra sau chốt

Thu CLS (`.../cls-batches/{serviceOrderId}/collect-cash`), thu phí khám qua `ICounterExamFeeCollector` và chi hoàn (`.../cash-refunds/{id}/disburse`) khóa cùng cashier/date ở đầu transaction, trước khóa booking/charge/refund. Ngày đã chốt trả 409 `CashierShiftClosed`, kể cả request thu đã từng thành công. Tiếp tân khác và ngày khác vẫn hoạt động. Cách ghi phiếu và các thao tác VNPAY online giữ nguyên.

Ngày DB đổi trong khi chờ khóa/đọc summary trả 409 `CashierShiftDateChanged`, rollback; client tải lại và gửi lại theo ngày mới. DI dùng `IDbClock`; constructor test cũ không truyền clock giữ fallback TimeProvider. Audit thu/hoàn dùng cùng timestamp của phiếu để phép đếm dựa vào ngày audit không lệch với ngày phiếu. Không đổi số phiếu, amount, status hay allocation.

## Ánh xạ cho frontend

| Web gọi | Backend / khác biệt |
|---|---|
| POST `/api/billing/staff/cashier/close-shift` | Cùng route/body/header; response thêm các field trên; xử lý 409 closed/idem/date changed |
| GET `/api/billing/staff/cashier/shift-summary` | Cùng route; thêm isClosed/closedAt, expectedCash*, cashierName và bản chụp |
| POST `/api/billing/staff/cashier/items/{id}/collect-cash` | Dùng POST `/api/billing/staff/cashier/cls-batches/{serviceOrderId}/collect-cash`; id phải là ServiceOrderId của đợt từ list, response CashReceipt hiện có |

Không cần schema/migration hoặc sửa lõi. Không sửa DI Infrastructure/BillingModule của PR #161; Ports chỉ sửa ICashierStore theo ủy quyền bổ sung, giữ IPriceCatalog/nhánh #165. UI/E2E và kết quả sau gộp các PR chưa kiểm chứng.
