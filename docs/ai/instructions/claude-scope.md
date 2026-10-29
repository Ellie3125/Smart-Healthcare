# claude-scope

## Phạm vi
Agent Claude Code; mọi lệnh shell, chỉnh sửa, tạo/xóa/format/generate.

## Khi nào đọc
Mỗi phiên Claude Code và trước thực thi công cụ ghi file.

## Quy tắc
- **OWN-07** — Claude Code sở hữu chatbot (ml-service/**, tests/ml/**, docs/ml/**), cấu hình Cloudflare Access/Tunnel (infrastructure/cloudflare/**), docs/security/**, CLAUDE.md, .claude/** và hồ sơ docs/tasks|sessions|evidence/claude/**, docs/handoffs/claude-to-*/**. Quyền file chính xác nằm ở ownership.md.
- **OWN-08** — Claude Code không tạo, sửa, xóa hoặc định dạng frontend/**, mobile/**, tests/e2e/**, docs/ui/** (Antigravity) hay backend/**, docker-compose.yml (kể cả service chatbot và ollama), .env.example, docs/api/** và hạ tầng ngoài infrastructure/cloudflare (Codex). Có thể đọc để tích hợp và chẩn đoán.
- **OWN-09** — Thiếu API, contract hoặc thay đổi compose thì ghi handoff claude-to-codex; thiếu UI chat thì ghi claude-to-antigravity, theo API-03. Tiếp tục phần độc lập, báo pending, không sửa hộ. Sửa ngoài scope chỉ khi trưởng nhóm ủy quyền ghi nguyên văn trong task file; quyền đó hết khi task kết thúc.

## Điều cấm
Không sửa nhỏ ngoài scope để test xanh, không sửa API client hay lockfile của bên khác. Không dùng .claude/settings.json hoặc skill để tự mở rộng quyền. Không đọc .env hay secret thật.

## Cách kiểm chứng
Đối chiếu toàn bộ diff theo docs/ai/check_ownership.md và ownership.md; .claude/settings.json chặn ghi vào frontend/**, backend/**, tests/e2e/**, docs/api/** như lớp phòng thủ thêm, không thay review.

## Nguồn
Ủy quyền của trưởng nhóm trong docs/tasks/claude/CLA-GOV-001.md; Giải pháp kỹ thuật v2.1 §8.7, §11; mẫu codex-scope.md.
