# messaging-rabbitmq

## Phạm vi
Codex: outbox, publisher và subscriber.

## Khi nào đọc
Thêm integration event, retry hoặc demo Buổi 12.

## Quy tắc
- **MSG-01** — Luồng đề xuất AppointmentConfirmed→Notifications và Reporting, mỗi subscriber có queue riêng; hai worker một queue không phải hai subscriber cùng nhận event.
- **MSG-02** — Commit dữ liệu nghiệp vụ và outbox cùng transaction; publisher retry/confirm, consumer ack sau commit, idempotency theo event_id, retry giới hạn + DLQ; durable queue/persistent message phù hợp.
- **MSG-03** — Event có event_id, event_type/version, occurred_at, correlation_id, aggregate_id; payload tối thiểu, không bệnh án. Xử lý duplicate/out-of-order/version và replay bằng contract.
- **MSG-04** — Buổi 12 cần một lời gọi đồng bộ REST hoặc gRPC giữa hai module nghiệp vụ và hai consumer. Dùng lab adapter Scheduling→Availability có thao tác kiểm-và-giữ nguyên tử; mặc định monolith gọi in-process. Backend→ML không tự thay thế demo giữa hai module.
- **MSG-05** — Event không thay kiểm capacity nguyên tử. Không tuyên bố exactly-once; chứng minh duplicate không nhân đôi side effect. E-commerce→Healthcare cần giảng viên xác nhận ánh xạ, ghi pending.

## Điều cấm
Không để consumer trừ slot sau khi đã xác nhận lịch để giải quyết tranh chấp. Không biến mọi module thành microservice.

## Cách kiểm chứng
Tắt consumer B→publish→A xử lý→B restart nhận tồn queue; gửi lại event không lặp side effect; publisher crash sau confirm trước đánh dấu outbox; consumer crash sau commit trước ack; DLQ poison message.

## Nguồn
Buổi 12 tr.2–5; Master H7; RabbitMQ official docs.
