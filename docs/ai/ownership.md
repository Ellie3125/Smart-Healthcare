# Quyền sửa file

Đối chiếu bảng này trước khi tạo, sửa, xóa, đổi tên, format hoặc chạy generator. Quyền thao tác thực tế là giao của owner đường dẫn, scope task và quyền công cụ. Được đọc code bên kia để hiểu tích hợp.

| Đường dẫn trong repository | Agent được sửa |
|---|---|
| `frontend/**` | Antigravity |
| `mobile/**` | Antigravity |
| `tests/e2e/**` | Antigravity |
| `docs/ui/**` | Antigravity |
| `docs/handoffs/antigravity-to-codex/**` | Antigravity |
| `docs/handoffs/antigravity-to-claude/**` | Antigravity |
| `docs/tasks/antigravity/**` | Antigravity |
| `docs/sessions/antigravity/**` | Antigravity |
| `docs/evidence/antigravity/**` | Antigravity |
| `ml-service/**` | Claude Code |
| `tests/ml/**` | Claude Code |
| `docs/ml/**` | Claude Code |
| `infrastructure/cloudflare/**` | Claude Code |
| `docs/security/**` | Claude Code |
| `CLAUDE.md` | Claude Code |
| `.claude/**` | Claude Code |
| `docs/handoffs/claude-to-codex/**` | Claude Code |
| `docs/handoffs/claude-to-antigravity/**` | Claude Code |
| `docs/tasks/claude/**` | Claude Code |
| `docs/sessions/claude/**` | Claude Code |
| `docs/evidence/claude/**` | Claude Code |
| `backend/**` | Codex |
| `infrastructure/**` (trừ `infrastructure/cloudflare/**`) | Codex |
| `scripts/**` | Codex |
| `tests/backend/**` | Codex |
| `tests/integration/**` | Codex |
| `tests/load/**` | Codex |
| `docs/api/**` | Codex |
| `docs/business/**` | Codex |
| `docs/architecture/**` | Codex |
| `docs/uml/**` | Codex |
| `docs/operations/**` | Codex |
| `docs/testing/**` | Codex |
| `docs/reports/**` | Codex |
| `docs/adr/**` | Codex |
| `docs/process/**` | Codex |
| `docs/ai/**` | Codex |
| `docs/standards/**` | Codex |
| `docs/handoffs/codex-to-antigravity/**` | Codex |
| `docs/handoffs/codex-to-claude/**` | Codex |
| `docs/tasks/codex/**` | Codex |
| `docs/sessions/codex/**` | Codex |
| `docs/evidence/codex/**` | Codex |
| `.agents/**` | Codex |
| `.github/**` | Codex |
| `AGENTS.md` | Codex |
| `GEMINI.md` | Codex |
| `README.md` | Codex |
| `.gitignore` | Codex |
| `.gitattributes` | Codex |
| `Jenkinsfile` | Codex |
| `compose.yaml` | Codex |
| `compose.dev.yaml` | Codex |
| `compose.staging.yaml` | Codex |
| `compose.prod.yaml` | Codex |
| `.env.example` | Codex |
| `.editorconfig` | Codex |
| `global.json` | Codex |
| `Directory.Build.props` | Codex |
| `Directory.Packages.props` | Codex |
| `NuGet.Config` | Codex |

`/**` áp dụng cho mọi file con; dòng có đường dẫn cụ thể hơn thắng dòng bao quát. File chưa thuộc bảng phải được xác định owner trước khi sửa; không tự suy thành file dùng chung. Các tên như global.json, compose.yaml và Jenkinsfile trong bảng là file ứng dụng/hạ tầng tương lai, không phải file quy tắc phải đổi đuôi.

Service `chatbot` và `ollama` trong compose.yaml và các file override compose.*.yaml vẫn do Codex sửa; Claude Code cần thay đổi thì gửi handoff claude-to-codex. Đề xuất đổi tên `ml-service` thành `chatbot-service` chỉ được ghi nhận, chưa áp dụng.

Codex không sửa mã UI dù đặt ngoài frontend/** hoặc mobile/**. Antigravity không sửa logic nghiệp vụ server, DB/migration, chatbot, hạ tầng hoặc pipeline dù file được chuyển tên. Claude Code không sửa UI, Backend, file compose, docs/api hay hạ tầng ngoài infrastructure/cloudflare. Formatter/generator và lockfile cũng chịu cùng ranh giới.

Khi cần bên kia sửa, ghi handoff đúng chiều; không sửa thay. Thay đổi owner/chính sách cần trưởng nhóm review, không sửa luật để hợp thức hóa diff đã vượt scope. Ủy quyền ngoại lệ của trưởng nhóm phải ghi trong task file và hết hạn khi task kết thúc.

Đây là quy tắc để agent/người review đọc và đối chiếu; không tự chặn thao tác filesystem hoặc tự tạo gate Jenkins. Kiểm thay đổi theo check_ownership.md.
