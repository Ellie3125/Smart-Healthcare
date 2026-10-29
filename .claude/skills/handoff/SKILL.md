---
name: handoff
description: "Dùng khi Claude Code cần Codex hoặc Antigravity sửa phần ngoài scope (API, contract, compose, UI chat): viết handoff claude-to-codex hoặc claude-to-antigravity."
---

# Viết handoff

1. Đọc api-contract-and-handoff.md (API-03) và claude-scope.md (OWN-09). Xác định bên nhận theo ownership.md.
2. Tạo docs/handoffs/claude-to-codex/<task-id>.md hoặc docs/handoffs/claude-to-antigravity/<task-id>.md. Không sửa handoff của bên khác; phản hồi của họ nằm trong codex-to-claude/ hoặc antigravity-to-claude/.
3. Ghi đủ các trường API-03:
   - task / use case và requirement liên quan
   - contract commit/hash (hoặc "chưa có contract")
   - trạng thái BE / UI-mock / UI-real / chatbot / E2E
   - kết quả đã chạy (command, exit code) hoặc Not run
   - thiếu sót cần bên nhận làm, kèm acceptance criteria
   - next_action
4. Ghi đường dẫn handoff vào task và session của mình.

## Checklist kiểm chứng
- [ ] Đủ sáu trường API-03, không bỏ trống.
- [ ] Không kèm bản sửa sẵn file của bên nhận.
- [ ] Không chứa secret hay dữ liệu bệnh nhân.
