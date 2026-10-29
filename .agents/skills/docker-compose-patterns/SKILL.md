---
name: docker-compose-patterns
description: "Tạo, review hoặc debug Docker Compose, service readiness, volume, network và multi-instance Smart Healthcare; dành cho Codex."
license: "Complete terms in LICENSE.md"
---

# Compose Smart Healthcare

1. Đọc linux-docker-compose.md, sqlserver-efcore.md và scope Codex. Dùng compose.yaml làm base cùng override compose.dev.yaml (laptop), compose.staging.yaml (VPS Staging), compose.prod.yaml (VPS Prod) và .env riêng từng môi trường; Staging giống Prod, chỉ khác secrets/dữ liệu/hostname. CD dùng image đã build, không rebuild source.
2. Ghép Nginx, hai Backend cùng image digest, worker .NET Hangfire và SQL Server theo luồng cần thiết; service chatbot và ollama nằm trong profile `chatbot`, bật ở Staging và Prod sau cùng. Không có object storage tự host hay message broker: tệp ở Cloudflare R2, tác vụ nền qua Outbox. Frontend build tĩnh do Antigravity tạo; Codex cấu hình Nginx để phục vụ artifact, không sửa UI.
3. Dùng tên service cho kết nối nội bộ, network theo trust boundary. Không publish SQL Server/chatbot/Ollama/Backend trực tiếp; lab cổng Backend riêng chỉ bind loopback. Đường vào từ ngoài qua Cloudflare Tunnel; Jenkins theo mạng quản trị có hạn chế.
4. Healthcheck theo client/tool thật có trong image; SQL Server không dùng probe của DB khác. depends_on service_healthy cho phụ thuộc bắt buộc, migration one-shot có kiểm soát. Chatbot lỗi/chưa bật không được chặn manual booking. Runtime reconnect/retry có giới hạn.
5. Named volumes bảo vệ SQL Server và model Ollama khi recreate, nhưng không là backup. Secrets ngoài repo/image; config production có thể bind mount read-only. Không hard-code credential, không in cấu hình đã resolve secrets.
6. Log rotation, resource limits và restart policy có lý do. Running khác ready; unhealthy không có nghĩa Docker tự restart. Trạng thái slot/idempotency/session quan trọng không ở RAM riêng từng Backend.
7. Dùng docker compose -f compose.yaml -f compose.<env>.yaml config --quiet cho từng env (thêm --profile chatbot cho Staging/Prod), rồi kiểm readiness, hai instance nhận request, dữ liệu tồn sau recreate và private ports từ bên ngoài khi được phép. Chỉ coi là pass phần đã chạy.
8. Không dùng down -v, volume prune hoặc reset làm bước sửa lỗi mặc định. Chỉ xóa dữ liệu theo đích/ủy quyền rõ và có phương án phục hồi; không dọn tài nguyên người khác.

Khi có compose.yaml thật, dùng Docker Compose kiểm cấu hình và chạy các bước kiểm chứng theo task. Đọc skill không chứng minh stack đã chạy hoặc đáp ứng yêu cầu nhiều máy.

Nguồn: https://github.com/docker/skills/tree/f79172733725e2fd236343ee7b963c446ed85d09/skills/docker-compose-patterns. Bản hướng dẫn đã được biên tập cho Smart Healthcare, ngày 16/10/2026, cập nhật theo Giải pháp kỹ thuật v2.1 ngày 29/10/2026; giữ license tác giả trong LICENSE.md.
