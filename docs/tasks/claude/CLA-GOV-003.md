# CLA-GOV-003 — Cập nhật Giải pháp kỹ thuật lên v2.2

Yêu cầu của trưởng nhóm trong chat ngày 29/10/2026 (nguyên văn): "cập nhật lại file giải pháp trong phần dowload tôi mới có thêm bản mới". Làm theo quyền giám sát OWN-10 vì docs/architecture/** và docs/ai/** thuộc Codex.

- task_id: CLA-GOV-003
- owner_agent: claude
- owner_member: Phạm Hoà (Ellie3125, trưởng nhóm)
- base_commit: 3b5cd80
- branch: ellie3125/claude-governance
- requirement_ids: GOV-02, GOV-06, OWN-10; Giải pháp kỹ thuật v2.2 §2.1, §7, §11, §13.1 bước 7, §13.2, §14
- allowed_paths: docs/architecture/giai-phap-ky-thuat-v2.1.md → v2.2.md, AGENTS.md, docs/ai/**, .agents/skills/docker-compose-patterns/SKILL.md, .claude/skills/chatbot-finetune-eval/SKILL.md, ml-service/README.md, docs/tasks/claude/**, docs/handoffs/claude-to-codex/**
- contract_version: không áp dụng
- acceptance_criteria: docs/architecture/giai-phap-ky-thuat-v2.2.md giống hệt bản "Giải pháp kỹ thuật Smart Healthcare v2.2.md" trong Downloads/Context-Smart-Healthcare (cmp); file v2.1 đổi tên thành v2.2 (git mv); mọi tham chiếu sống trỏ v2.2; BOT-07/BOT-09, skill chatbot-finetune-eval, README ml-service theo lộ trình §11.2 và bốn lớp §11.3–11.4; handoff báo Codex; tự kiểm đạt.
- status: In review
- depends_on: CLA-GOV-002
- next_action: Trưởng nhóm review và cho push; Codex đọc docs/handoffs/claude-to-codex/CLA-GOV-003.md.

## Ghi chú
- Phạm vi nghiệp vụ trong Downloads/Context-Smart-Healthcare vẫn là v2.1 và giống hệt bản trong repo; không đổi.
- Hồ sơ lịch sử (CLA-GOV-001 task/session/evidence) giữ tên v2.1 như lúc ghi.
- Kiểm chứng: `cmp` bản repo với bản Downloads → giống; `python -I -X utf8 check_refs.py .` (script trong docs/evidence/claude/CLA-GOV-001-selfcheck.md) → exit 0.
