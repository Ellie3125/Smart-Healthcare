# linux-docker-compose

## Phạm vi
Codex: Linux/WSL, Dockerfile, Compose và mạng nội bộ.

## Khi nào đọc
Docker hóa, ghép service, debug startup hoặc vận hành dữ liệu.

## Quy tắc
- **OPS-01** — SQL Server, chatbot và Ollama không publish port host; Ollama chỉ mở trong mạng Docker nội bộ. Backend prod chỉ sau Nginx và Cloudflare Tunnel, origin không mở cổng ứng dụng ra Internet; lab hai cổng riêng chỉ bind loopback. Phân đoạn network theo luồng thật; kiểm tra cả IPv4/IPv6 từ bên ngoài.
- **CTR-01** — Demo user/group/chown/chmod/apt/nano/curl trên Backend thật, chạy non-root. Không dùng chmod 777 làm mặc định; quyền Docker socket gần tương đương root, phải ghi rõ.
- **CTR-02** — Dockerfile multi-stage + .dockerignore, runtime non-root; giữ license cần phân phối; .agents/docs không vào runtime. Hai Backend từ cùng image digest; session/hold/idempotency không nằm riêng trong RAM.
- **CTR-03** — Một docker-compose.yml dùng chung cho laptop và VPS; khác biệt môi trường chỉ qua .env (biến không bí mật) và secrets ngoài repo, không dùng file override dev/staging/prod. Service chatbot và ollama nằm trong profile `chatbot`, bật sau cùng khi API lịch/vé/thanh toán ổn định; khi chưa bật, API chat trả chưa hỗ trợ và chức năng khác vẫn chạy. Healthcheck native phù hợp SQL Server/image thực tế; depends_on đúng readiness cho phụ thuộc bắt buộc. Chatbot không là điều kiện chặn Backend hoặc đặt lịch thủ công.
- **CTR-04** — Named volumes cho SQL Server và model Ollama (tệp kết quả nằm ở R2, không ở volume cục bộ), restart policy/log rotation/resource limits; phân biệt running, ready và healthy. Unhealthy không có nghĩa Docker tự restart, volume không là backup.
- **CTR-05** — Secrets ở host/credential store, read-only mount hoặc cơ chế runtime phù hợp; không trong ARG/ENV build hay repository. Bind mount config read-only trong production được phép trong Staging/Production.
- **CTR-06** — Chỉ lệnh xóa dữ liệu có đích rõ và đã được ủy quyền mới chạy. Không tự down -v/prune/reset; sao lưu và phương án phục hồi trước thử nghiệm destructive.

## Điều cấm
Không copy asset PostgreSQL/Redis từ skill làm stack của dự án; không thêm sidecar hoặc distroless mù quáng. Không đọc in toàn bộ compose config đã resolve secret vào log.

## Cách kiểm chứng
docker compose config --quiet và docker compose --profile chatbot config --quiet; build/inspect user; gọi API thật; tạo dữ liệu→recreate container không xóa volume→dữ liệu còn; request qua hai instance; kiểm port từ ngoài. Lệnh runtime chưa chạy trong kit này.

## Nguồn
Buổi 5 tr.2; Buổi 6 tr.1–2; Buổi 7 tr.1–5. Master H2 và III SQL Server; docker skill snapshot. Giải pháp kỹ thuật v2.1 §8.6–8.7, §13.1 bước 7, §14.
