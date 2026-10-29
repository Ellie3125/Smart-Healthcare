# codex-scope

## Phạm vi
Agent Codex; mọi lệnh shell, chỉnh sửa, tạo/xóa/format/generate.

## Khi nào đọc
Mỗi phiên Codex và trước thực thi công cụ ghi file.

## Quy tắc
- **OWN-01** — Codex không được tạo, sửa, xóa hoặc định dạng frontend/**, mobile/** hay mã UI ở bất kỳ đường dẫn nào; tests/e2e/** và docs/ui/** thuộc Antigravity. Có thể đọc để chẩn đoán.
- **OWN-02** — Codex sở hữu backend, worker, infrastructure (trừ infrastructure/cloudflare), scripts, tests/backend, tests/integration, tests/load, docker-compose.yml (gồm service chatbot và ollama), .env.example, contract và tài liệu nghiệp vụ/kiến trúc. Chatbot (ml-service, tests/ml, docs/ml), docs/security và cấu hình Cloudflare Access/Tunnel thuộc Claude Code. Quyền file chính xác nằm ở ownership.md.
- **OWN-03** — Nếu phát hiện lỗi UI, tạo handoff codex-to-antigravity; lỗi chatbot, tạo codex-to-claude; gồm tái hiện, contract, ảnh hưởng và acceptance criteria. Tiếp tục phần Backend độc lập; báo UI pending, không sửa hộ.

## Điều cấm
Không có ngoại lệ sửa nhỏ, đổi lockfile, sửa API client sinh tự động hoặc sửa UI để build xanh. Không chuyển mã UI sang thư mục khác để tránh kiểm tra.

## Cách kiểm chứng
Đối chiếu toàn bộ diff theo docs/ai/check_ownership.md và ownership.md; review cả nội dung để phát hiện UI đặt sai đường dẫn.

## Nguồn
Master phần IV §2,4,6,9.
