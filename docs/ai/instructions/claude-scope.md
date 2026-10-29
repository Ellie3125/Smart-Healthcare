# claude-scope

## Phạm vi
Agent Claude Code; mọi lệnh shell, chỉnh sửa, tạo/xóa/format/generate.

## Khi nào đọc
Mỗi phiên Claude Code và trước thực thi công cụ ghi file.

## Quy tắc
- **OWN-07** — Claude Code sở hữu chatbot (ml-service/**, tests/ml/**, docs/ml/**), cấu hình Cloudflare Access/Tunnel (infrastructure/cloudflare/**), docs/security/**, CLAUDE.md, .claude/** và hồ sơ docs/tasks|sessions|evidence/claude/**, docs/handoffs/claude-to-*/**. Quyền file chính xác nằm ở ownership.md.
- **OWN-08** — Claude Code không tạo, sửa, xóa hoặc định dạng frontend/**, mobile/**, tests/e2e/**, docs/ui/** (Antigravity) hay backend/**, file compose (kể cả service chatbot và ollama), .env.example, docs/api/** và hạ tầng ngoài infrastructure/cloudflare (Codex), trừ khi làm theo OWN-10. Có thể đọc để tích hợp và chẩn đoán.
- **OWN-09** — Thiếu API, contract hoặc thay đổi compose thì ghi handoff claude-to-codex; thiếu UI chat thì ghi claude-to-antigravity, theo API-03. Tiếp tục phần độc lập, báo pending; tự sửa hộ chỉ khi trưởng nhóm giao trong phiên theo OWN-10.
- **OWN-10** — Claude Code có quyền giám sát thường trực (trưởng nhóm ủy quyền, docs/tasks/claude/CLA-GOV-002.md): được sửa mọi thư mục của Codex và Antigravity, lập task cho hai agent trong docs/tasks/codex|antigravity/ và review diff của họ, nhưng chỉ khi trưởng nhóm giao việc trong phiên. Codex và Antigravity vẫn là owner mặc định. Chống đụng độ: (1) trước khi sửa, kiểm task và branch đang mở của owner; file đang có task của bên kia thì không chen; (2) làm trên branch của Claude Code, ghi task trong docs/tasks/claude/; (3) sau khi sửa, ghi handoff claude-to-codex hoặc claude-to-antigravity nêu file và lý do để owner không ghi đè. Chatbot vẫn là việc chính; không push/merge/deploy khi chưa được phép.

## Điều cấm
Không sửa nhỏ ngoài scope để test xanh, không sửa API client hay lockfile của bên khác. Không dùng .claude/settings.json hoặc skill để tự mở rộng quyền ngoài OWN-10. Không đọc .env hay secret thật.

## Cách kiểm chứng
Đối chiếu toàn bộ diff theo docs/ai/check_ownership.md và ownership.md; với thay đổi theo OWN-10 kiểm thêm task CLA tương ứng và handoff báo owner. .claude/settings.json chỉ chặn đọc .env.

## Nguồn
Ủy quyền của trưởng nhóm trong docs/tasks/claude/CLA-GOV-001.md và CLA-GOV-002.md; Giải pháp kỹ thuật v2.2 §8.7, §11; mẫu codex-scope.md.
