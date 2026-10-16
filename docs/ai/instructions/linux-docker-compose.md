# linux-docker-compose

## Phạm vi
Codex: Linux/WSL, Dockerfile, Compose và mạng nội bộ.

## Khi nào đọc
Docker hóa, ghép service, debug startup hoặc vận hành dữ liệu.

## Quy tắc
- **OPS-01** — Database và ML không publish port host. Backend prod chỉ sau Nginx; lab hai cổng riêng chỉ bind loopback. Phân đoạn network theo luồng thật; kiểm tra cả IPv4/IPv6 từ bên ngoài.
- **CTR-01** — Demo user/group/chown/chmod/apt/nano/curl trên Backend thật, chạy non-root. Không dùng chmod 777 làm mặc định; quyền Docker socket gần tương đương root, phải ghi rõ.
- **CTR-02** — Dockerfile multi-stage + .dockerignore, runtime non-root; giữ license cần phân phối; .agents/docs không vào runtime. Hai Backend từ cùng image digest; session/hold/idempotency không nằm riêng trong RAM.
- **CTR-03** — Compose base và override dev/staging/prod rõ; healthcheck native phù hợp SQL Server/image thực tế; depends_on đúng readiness cho phụ thuộc bắt buộc. ML không là điều kiện chặn Backend manual booking.
- **CTR-04** — Named volumes cho DB/storage/broker, restart policy/log rotation/resource limits; phân biệt running, ready và healthy. Unhealthy không có nghĩa Docker tự restart, volume không là backup.
- **CTR-05** — Secrets ở host/credential store, read-only mount hoặc cơ chế runtime phù hợp; không trong ARG/ENV build hay repository. Bind mount config read-only trong production được phép trong Staging/Production.
- **CTR-06** — Chỉ lệnh xóa dữ liệu có đích rõ và đã được ủy quyền mới chạy. Không tự down -v/prune/reset; sao lưu và phương án phục hồi trước thử nghiệm destructive.

## Điều cấm
Không copy asset PostgreSQL/Redis từ skill làm stack của dự án; không thêm sidecar hoặc distroless mù quáng. Không đọc in toàn bộ compose config đã resolve secret vào log.

## Cách kiểm chứng
docker compose config --quiet; build/inspect user; gọi API thật; tạo dữ liệu→recreate container không xóa volume→dữ liệu còn; request qua hai instance; kiểm port từ ngoài. Lệnh runtime chưa chạy trong kit này.

## Nguồn
Buổi 5 tr.2; Buổi 6 tr.1–2; Buổi 7 tr.1–5. Master H2 và III SQL Server; docker skill snapshot.
