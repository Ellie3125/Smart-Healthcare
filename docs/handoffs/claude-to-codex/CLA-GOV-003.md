# Handoff CLA-GOV-003 — Claude Code đã sửa vùng của Codex (OWN-10)

Trưởng nhóm giao trong chat ngày 29/10/2026 việc cập nhật Giải pháp kỹ thuật lên v2.2. Claude Code đã sửa các file thuộc Codex dưới đây trên nhánh ellie3125/claude-governance; đọc diff trước khi làm tiếp, không revert khi chưa hỏi trưởng nhóm.

## File đã sửa
- `docs/architecture/giai-phap-ky-thuat-v2.1.md` → `giai-phap-ky-thuat-v2.2.md` (git mv, nội dung là bản v2.2 trưởng nhóm cung cấp).
- `AGENTS.md`, `docs/ai/instructions/*.md`, `docs/ai/check_governance.md`, `.agents/skills/docker-compose-patterns/SKILL.md`: tham chiếu v2.1 → v2.2; GOV-02 và AGENTS.md ghi giai đoạn 1 dùng model nền chưa fine-tune.

## Thay đổi v2.2 ảnh hưởng tới Codex
1. Mục 11 đánh số lại: xác nhận nghiệp vụ của chatbot nay là §11.5 (cũ 11.2), cache danh mục và tra thuốc là §11.6 (cũ 11.3).
2. Short-term Memory (§11.4.1) cần bộ lưu checkpoint bền, dùng chung cho mọi bản chạy chatbot ở Staging và Prod (RAM chỉ khi dev). Chưa chọn bộ lưu; khi thiết kế compose cho profile `chatbot`, đề xuất phương án cho trưởng nhóm chốt rồi khai báo service/volume tương ứng.
3. ChatContext (§11.3.3) cần .NET truyền `user_id`, `patient_id`, `conversation_id` và access token của phiên khi gọi FastAPI; đưa vào contract API chat ở mục 5 handoff CLA-GOV-001.
4. ProposalOut (§11.3.1): đề nghị chờ xác nhận do .NET lưu gồm `proposal_id`, `action` (Book/Reschedule/Cancel), `summary`, `expires_at_utc`; các DTO SlotOption, AppointmentOut, TicketOut, ExamPaymentOut là gợi ý, Codex chốt tên trường trong contract.

## Acceptance criteria phía Codex
- Contract API chat và công cụ chatbot phản ánh mục 3–4.
- Compose profile `chatbot` có bộ lưu checkpoint đã được trưởng nhóm chốt.
