# CLA-GOV-002 — Quyền giám sát thường trực của Claude Code

## Ủy quyền của trưởng nhóm (chép nguyên văn, chat ngày 29/10/2026)
"tôi cho bạn quyền cao nhất trên agent kia có thể cân thiệt vô thư mục dành cho 2 agent kia, như vẫn không quân nhiệm vụ bên chatbot"

Claude Code đề xuất ghi quyền này thường trực vào luật (không chỉ theo từng task); trưởng nhóm trả lời: "oke làm thế đi". Nghĩa đã thống nhất: "can thiệp vào thư mục dành cho 2 agent kia", "nhưng vẫn không quên nhiệm vụ bên chatbot". Thường trực nghĩa là quyền ghi trong luật; Claude Code chỉ hành động trong phiên khi trưởng nhóm giao việc, không chạy ngầm.

## Trường GOV-03
- task_id: CLA-GOV-002
- owner_agent: claude
- owner_member: Phạm Hoà (Ellie3125, trưởng nhóm)
- base_commit: db7447d
- branch: ellie3125/claude-governance
- requirement_ids: GOV-06, OWN-07..10
- allowed_paths: AGENTS.md, docs/ai/ownership.md, docs/ai/check_ownership.md, docs/ai/instructions/claude-scope.md, docs/ai/instructions/codex-scope.md, docs/ai/instructions/antigravity-ui-scope.md, .claude/settings.json, .claude/rules/boundaries.md, docs/tasks/claude/**
- contract_version: không áp dụng
- acceptance_criteria: OWN-10 mô tả quyền giám sát và luật chống đụng độ; OWN-08/09, ownership.md, AGENTS.md, check_ownership.md, codex-scope.md, antigravity-ui-scope.md, .claude/rules/boundaries.md không còn câu cấm trái OWN-10; settings.json bỏ deny Edit, giữ deny đọc .env; không trùng rule ID; chatbot vẫn là việc chính của Claude Code.
- status: In review
- depends_on: CLA-GOV-001
- next_action: Trưởng nhóm review; tiếp tục CLA-CHAT-000 (chốt phiên bản, tạo pyproject).

## Giữ nguyên
Không push/merge/deploy khi chưa được trưởng nhóm cho phép; không đọc .env/secret thật; luật nghiệp vụ y tế và contract-first giữ nguyên. Codex và Antigravity vẫn là owner mặc định của vùng mình.
