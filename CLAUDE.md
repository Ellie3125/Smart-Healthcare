@AGENTS.md
@docs/ai/instructions/claude-scope.md
@docs/ai/instructions/project-governance.md
@docs/ai/ownership.md

# Claude Code — Smart Healthcare

Claude Code là agent thứ ba, cùng Codex và Antigravity. Luật hiện hành nằm ở các file được nạp phía trên và trong docs/ai/instructions/; file này chỉ tham chiếu, không chép lại luật. Luật nạp theo đường dẫn nằm trong .claude/rules/, skill riêng nằm trong .claude/skills/.

## Bắt đầu phiên
1. Xác nhận đang chạy Claude Code. Đọc healthcare-domain.md, testing-and-evidence.md và instruction theo bảng "Đọc theo nhiệm vụ" trong AGENTS.md (chatbot: chatbot-langchain-ollama.md; Access/Tunnel: nginx-cloudflare.md).
2. Kiểm git status, branch và base commit; đọc task trong docs/tasks/claude/, handoff đến trong docs/handoffs/codex-to-claude/ và docs/handoffs/antigravity-to-claude/, contract trong docs/api/. Giữ thay đổi của người khác.
3. Chưa có task thì lập task thiết kế tại docs/tasks/claude/<task-id>.md đủ trường GOV-03; không giả tài liệu đã tồn tại.
4. Báo task ID, allowed_paths, contract version, acceptance criteria và cách kiểm chứng trước khi ghi file.

## Kết thúc phiên
1. Chạy skill verification-before-completion và test liên quan thật; ghi command/cwd/exit code/môi trường.
2. Tự kiểm theo docs/ai/check_ownership.md; nếu sửa bộ luật thì thêm docs/ai/check_governance.md.
3. Ghi session vào docs/sessions/claude/<task-id>-<session-id>.md và evidence thật vào docs/evidence/claude/ theo GOV-07.
4. Phần thuộc bên khác ghi handoff vào docs/handoffs/claude-to-codex/ hoặc docs/handoffs/claude-to-antigravity/ theo API-03.
5. Cập nhật task với status và next_action cụ thể. Không push, merge hay mở PR khi chưa được trưởng nhóm cho phép.
