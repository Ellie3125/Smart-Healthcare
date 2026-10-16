# Smart Healthcare — quy chuẩn làm việc của agent

Đọc file này đầu mỗi phiên. Bộ luật hiện hành nằm trong docs/ai/instructions/; không áp dụng một bản luật khác từ trí nhớ chat.

## Công nghệ và trách nhiệm
ASP.NET Core + EF Core + SQL Server; Backend Modular Monolith, áp dụng Clean Architecture trong từng module theo backend-solid-architecture.md. Frontend React + Vite + TypeScript. ML Python/FastAPI nội bộ. Docker Compose, Nginx, Cloudflare DNS/proxy/TLS, Jenkins, MinIO làm object storage chính, RabbitMQ/outbox. Nginx phục vụ Frontend build và proxy API cùng origin tới hai Backend cùng image. VPN riêng tùy chọn, chưa chọn sản phẩm. Phiên bản/edition/license chốt trước triển khai.

Codex sở hữu Backend/nghiệp vụ/SQL Server/ML/hạ tầng/API contract. Codex tuyệt đối không tạo/sửa/xóa/format frontend/** hoặc mã UI, kể cả package/lockfile/API client. Antigravity sở hữu UI, component test và tests/e2e/**; không sửa Backend/DB/ML/hạ tầng/CI/CD. Được đọc code bên kia để hiểu tích hợp. Thiếu thay đổi bên kia thì bàn giao.

## Bắt đầu mỗi phiên
1. Xác định agent từ công cụ thực tế. Đọc project-governance.md, healthcare-domain.md, testing-and-evidence.md và file scope của mình trong docs/ai/instructions/.
2. Đọc docs/ai/ownership.md. Kiểm git status/branch/base commit, task/handoff/contract và quyết định thiết kế hiện có; giữ thay đổi của người khác. Nếu chưa có task, lập task thiết kế trong phạm vi của mình theo project-governance.md; không giả tài liệu đã tồn tại.
3. Chọn instruction và skill theo bảng dưới. Đọc SKILL.md trước dùng; đọc hướng dẫn trước áp dụng. Không tự cài thêm skill/hook hoặc gọi agent khác.
4. Báo task ID, allowed_paths, contract version, acceptance criteria và cách kiểm chứng. Chỉ code tính năng khi thiết kế/contract/scope rõ; thiếu thì hoàn thành phần thiết kế trước.

## Đọc theo nhiệm vụ
| Nhiệm vụ | Instructions trong docs/ai/instructions/ | Skill |
|---|---|---|
| Scope Codex | codex-scope.md | — |
| Scope Antigravity | antigravity-ui-scope.md | — |
| API/kiến trúc | backend-solid-architecture.md, database-module-boundaries.md, api-contract-and-handoff.md | — |
| SQL Server/EF Core | sqlserver-efcore.md | — |
| UI/React/E2E | frontend-react.md, api-contract-and-handoff.md | frontend-design cho UI mới |
| Training/inference | ml-training-serving.md | — |
| Dockerfile | linux-docker-compose.md | docker-build-strategies |
| Compose | linux-docker-compose.md | docker-compose-patterns |
| Proxy/TLS/hosting | nginx-cloudflare.md | — |
| File/MinIO | object-storage.md | — |
| CI/CD/release | jenkins-environments-release.md | — |
| Messaging | messaging-rabbitmq.md | — |
| VPN khi được chọn | vpn-network-administration.md | — |
| Hoàn thành/PR | testing-and-evidence.md | verification-before-completion |

Skill nằm trong .agents/skills/. Bốn skill đã được biên tập cho đúng dự án; nguồn/commit/license ở docs/ai/skill-manifest.md. Mỗi nhiệm vụ chọn một skill chính, chỉ thêm hỗ trợ cần thiết. Công việc không có skill dùng instructions và tài liệu chính thức phù hợp phiên bản.

## Kiểm tra và kết thúc phiên
Đọc docs/ai/check_governance.md để tự kiểm bộ hướng dẫn và docs/ai/check_ownership.md để review scope/diff trước bàn giao. Đây là checklist Markdown cho agent/người review, không tự thực thi hoặc chặn ghi file. Build/test ứng dụng chỉ chạy theo source và cấu hình thực tế khi đã có.
Chạy test liên quan tới thay đổi thật; ghi command/cwd/exit code/môi trường và phần chưa kiểm chứng. Cập nhật task/session/handoff theo project-governance.md; ghi next_action cụ thể. Không báo UI/Backend/E2E hoàn thành thay phần chưa được kiểm chứng.
