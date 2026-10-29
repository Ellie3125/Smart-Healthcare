# Handoff claude-to-codex — CLA-GOV-001

## Task / use case
CLA-GOV-001 cập nhật bộ quy chuẩn theo Phạm vi nghiệp vụ v2.1 và Giải pháp kỹ thuật v2.1 (docs/business/pham-vi-nghiep-vu-v2.1.md, docs/architecture/giai-phap-ky-thuat-v2.1.md). Luật đã được sửa; các phần dưới đây thuộc Codex nên Claude Code không tự làm.

## Contract commit/hash
Chưa có contract API (docs/api/ trống). Bộ luật tham chiếu: nhánh ellie3125/claude-governance, commit 44eeee0 trở về sau.

## Trạng thái BE / UI-mock / UI-real / chatbot / E2E
BE: chưa có code (chỉ cây thư mục .gitkeep). UI-mock, UI-real, chatbot, E2E: Not run.

## Kết quả đã chạy
Chỉ tự kiểm bộ luật (docs/evidence/claude/CLA-GOV-001-selfcheck.md). Chưa chạy build/test ứng dụng.

## Việc cần Codex làm
1. Cấu trúc Backend theo backend-solid-architecture.md (v2.1 §8.2.2):
   - backend/src/Api/ → backend/src/Bootstrapper/SmartHealthcare.Api/; thêm backend/src/Bootstrapper/SmartHealthcare.Worker/.
   - Gộp Modules/Identity và Modules/Patients thành Modules/IdentityPatients; gộp Documents và Reporting thành Modules/DocumentsReporting; thêm Modules/MedicationCatalog.
   - Đổi thư mục con Presentation/ thành <Module>.Api/ và đặt tên project <Module>.{Contracts,Domain,Application,Infrastructure,Api}.
   - backend/src/SharedKernel/ → backend/src/Shared/Shared.Abstractions/ và Shared.Infrastructure/; thêm tests/backend/ArchitectureTests/.
   - Acceptance: cây thư mục khớp bảng trong backend-solid-architecture.md; không còn Identity/, Patients/, Documents/, Reporting/, SharedKernel/, Presentation/ cũ.
2. Hạ tầng: bỏ infrastructure/minio/ và infrastructure/rabbitmq/ (không còn trong stack v2.1). Acceptance: không còn thư mục này.
3. Compose: tạo một docker-compose.yml dùng chung laptop và VPS, chỉ khác .env (CTR-03); service `chatbot` và `ollama` trong profile `chatbot`, Ollama không publish port, chỉ mạng nội bộ (OPS-01, BOT-08). Acceptance: `docker compose config --quiet` và `docker compose --profile chatbot config --quiet` exit 0; không có compose.*.yaml override.
4. .env.example: thêm `CHATBOT_URL` (địa chỉ nội bộ của chatbot cho .NET, ví dụ http://chatbot:8000) cùng các biến không bí mật khác. Acceptance: có `CHATBOT_URL`, không có secret thật.
5. API chat: khi profile `chatbot` chưa bật, endpoint chat của .NET trả trạng thái chưa hỗ trợ, các API khác vẫn chạy (v2.1 §13.1 bước 7). Contract cho các công cụ chatbot (tìm lịch, xem lịch của tôi, chuẩn bị đặt/đổi/hủy, xác nhận đề nghị, tra vé, xem thanh toán khám) cần được đề xuất trong docs/api/ trước khi Claude Code làm công cụ.

## next_action
Codex lập task trong docs/tasks/codex/ cho mục 1–4, phản hồi tại docs/handoffs/codex-to-claude/CLA-GOV-001.md; contract chatbot (mục 5) làm khi bắt đầu API lịch/vé/thanh toán.
