# Quyền sửa file

Đối chiếu bảng này trước khi tạo, sửa, xóa, đổi tên, format hoặc chạy generator. Quyền thao tác thực tế là giao của owner đường dẫn, scope task và quyền công cụ. Được đọc code bên kia để hiểu tích hợp.

| Đường dẫn trong repository | Agent được sửa |
|---|---|
| `frontend/**` | Antigravity |
| `tests/e2e/**` | Antigravity |
| `docs/ui/**` | Antigravity |
| `docs/handoffs/antigravity-to-codex/**` | Antigravity |
| `docs/tasks/antigravity/**` | Antigravity |
| `docs/sessions/antigravity/**` | Antigravity |
| `docs/evidence/antigravity/**` | Antigravity |
| `backend/**` | Codex |
| `ml-service/**` | Codex |
| `infrastructure/**` | Codex |
| `scripts/**` | Codex |
| `tests/backend/**` | Codex |
| `tests/integration/**` | Codex |
| `tests/ml/**` | Codex |
| `tests/load/**` | Codex |
| `docs/api/**` | Codex |
| `docs/business/**` | Codex |
| `docs/architecture/**` | Codex |
| `docs/uml/**` | Codex |
| `docs/ml/**` | Codex |
| `docs/security/**` | Codex |
| `docs/operations/**` | Codex |
| `docs/testing/**` | Codex |
| `docs/reports/**` | Codex |
| `docs/adr/**` | Codex |
| `docs/process/**` | Codex |
| `docs/ai/**` | Codex |
| `docs/standards/**` | Codex |
| `docs/handoffs/codex-to-antigravity/**` | Codex |
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

`/**` áp dụng cho mọi file con. File chưa thuộc bảng phải được xác định owner trước khi sửa; không tự suy thành file dùng chung. Các tên như global.json, compose.yaml và Jenkinsfile trong bảng là file ứng dụng/hạ tầng tương lai, không phải file quy tắc phải đổi đuôi.

Codex không sửa mã UI dù đặt ngoài frontend/**. Antigravity không sửa logic nghiệp vụ server, DB/migration, ML, hạ tầng hoặc pipeline dù file được chuyển tên. Formatter/generator và lockfile cũng chịu cùng ranh giới.

Khi cần bên kia sửa, ghi handoff đúng chiều; không sửa thay. Thay đổi owner/chính sách cần trưởng nhóm review, không sửa luật để hợp thức hóa diff đã vượt scope.

Đây là quy tắc để agent/người review đọc và đối chiếu; không tự chặn thao tác filesystem hoặc tự tạo gate Jenkins. Kiểm thay đổi theo check_ownership.md.
