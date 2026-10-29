# background-jobs-outbox

## Phạm vi
Codex: worker .NET, Hangfire với SQL Server, Operation, Outbox và integration event handler.

## Khi nào đọc
Thêm integration event, tác vụ nền, retry, đối soát hoặc demo Buổi 12.

## Quy tắc
- **MSG-01** — Tác vụ nền chạy trong worker .NET Service riêng (container riêng) cạnh BE1/BE2, dùng Hangfire Server với SQL Server storage và cùng contracts, transaction, khóa, rowversion, idempotency như API. Không xây scheduler hoặc hàng chờ job tự quản song song, không tạo bảng WorkflowJob sao chép Hangfire, không dùng BullMQ; Redis không giữ nguồn quyết định quota hay tình trạng bận. MVP chưa có message broker; broker chỉ được chọn ở bước tách service.
- **MSG-02** — Commit dữ liệu nghiệp vụ và Outbox trong cùng transaction. Worker quét Outbox chưa xử lý, enqueue/thực thi handler theo định danh ổn định rồi đánh dấu. Handler ghi EventId đã xử lý (ProcessedEvent) cùng transaction với tác dụng nghiệp vụ. Job chỉ mang định danh Operation/Outbox và tham chiếu tối thiểu; retry có giới hạn, quá giới hạn chuyển ManualReview, không lặp vô hạn.
- **MSG-03** — Integration event có EventId, loại và phiên bản, OccurredAtUtc, OperationId/correlation, aggregate id; payload tối thiểu, không bệnh án. Xử lý duplicate và sai thứ tự bằng version, đọc lại nguồn khi cần; PaymentFailed cũ không ghi đè Paid, ResultPublished muộn không kích hoạt vé của đợt chỉ định đã bị thay thế.
- **MSG-04** — Demo Buổi 12 dùng một lời gọi đồng bộ qua contract giữa hai module nghiệp vụ và hai handler cùng nhận một integration event (ví dụ AppointmentConfirmed → Notifications và Reporting), mỗi handler chống lặp riêng. Mặc định monolith gọi in-process; chatbot gọi .NET không thay thế demo giữa hai module.
- **MSG-05** — Event không thay kiểm capacity nguyên tử. Không tuyên bố exactly-once vì có Outbox; chứng minh duplicate không nhân đôi side effect. Channel/hàng chờ RAM chỉ để đánh thức worker; không tạo tác vụ quan trọng bằng Task.Run rồi trả thành công trước khi có bản ghi bền. Ánh xạ ví dụ e-commerce→Healthcare cần giảng viên xác nhận, ghi pending.

## Điều cấm
Không để handler trừ slot sau khi đã xác nhận lịch để giải quyết tranh chấp. Không gọi cổng thanh toán, model, R2 hay push trong transaction DB. Worker không tự chẩn đoán, tự duyệt kết quả, coi InProgress là hoàn tất hoặc cấp suất vượt trần. Không biến mọi module thành microservice.

## Cách kiểm chứng
Dừng worker→commit nghiệp vụ→Outbox còn→bật worker phát đúng; worker chết trước và sau side effect; cùng EventId gửi lại không lặp tác dụng; restart Web API khi worker đang xử lý; retry quá giới hạn vào ManualReview; callback và hết hạn chạy đồng thời chỉ một chuyển trạng thái thắng.

## Nguồn
Giải pháp kỹ thuật v2.1 §8.2–8.5, §9, §13.2 [T7][T8][T14]; Buổi 12 tr.2–5; Hangfire official docs.
