# Danh mục skill

Bốn skill trong .agents/skills/ được biên tập từ nguồn ngoài; .claude/skills/ có hai bản trỏ về bản gốc và năm skill Claude Code tự viết. Dùng tên trong frontmatter của SKILL.md; nguồn/commit/license ghi dưới đây. Chưa xác minh kích hoạt trong IDE của nhóm. Gói không có script thực thi, hook hoặc cơ chế tự gọi mạng.

## verification-before-completion
- Agent: codex, antigravity, claude (claude dùng bản trỏ `.claude/skills/verification-before-completion/SKILL.md`).
- Đường dẫn dùng: `.agents/skills/verification-before-completion/SKILL.md`.
- Repository nguồn: https://github.com/obra/superpowers.
- Thư mục nguồn: `skills/verification-before-completion`.
- Commit nguồn: `bb92a77741419a4ab5f06e711a283343f1ada0c3`.
- License: MIT; nội dung đầy đủ tại `.agents/skills/verification-before-completion/LICENSE.md`.
- Phạm vi: theo description của skill và ownership; skill không mở rộng quyền của agent.
- Phụ thuộc: chỉ instructions được định tuyến trong AGENTS.md; không tự cài skill hoặc tool khác.

## docker-compose-patterns
- Agent: codex.
- Đường dẫn dùng: `.agents/skills/docker-compose-patterns/SKILL.md`.
- Repository nguồn: https://github.com/docker/skills.
- Thư mục nguồn: `skills/docker-compose-patterns`.
- Commit nguồn: `f79172733725e2fd236343ee7b963c446ed85d09`.
- License: Apache-2.0; nội dung đầy đủ tại `.agents/skills/docker-compose-patterns/LICENSE.md`.
- Phạm vi: theo description của skill và ownership; skill không mở rộng quyền của agent.
- Phụ thuộc: chỉ instructions được định tuyến trong AGENTS.md; không tự cài skill hoặc tool khác.

## docker-build-strategies
- Agent: codex, claude (claude dùng bản trỏ `.claude/skills/docker-build-strategies/SKILL.md`, chỉ cho Dockerfile chatbot).
- Đường dẫn dùng: `.agents/skills/docker-build-strategies/SKILL.md`.
- Repository nguồn: https://github.com/docker/skills.
- Thư mục nguồn: `skills/docker-build-strategies`.
- Commit nguồn: `f79172733725e2fd236343ee7b963c446ed85d09`.
- License: Apache-2.0; nội dung đầy đủ tại `.agents/skills/docker-build-strategies/LICENSE.md`.
- Phạm vi: theo description của skill và ownership; skill không mở rộng quyền của agent.
- Phụ thuộc: chỉ instructions được định tuyến trong AGENTS.md; không tự cài skill hoặc tool khác.

## frontend-design
- Agent: antigravity.
- Đường dẫn dùng: `.agents/skills/frontend-design/SKILL.md`.
- Repository nguồn: https://github.com/anthropics/skills.
- Thư mục nguồn: `skills/frontend-design`.
- Commit nguồn: `dbd4588f9e1033efb41dad4bef2f7947c8993d44`.
- License: Apache-2.0; nội dung đầy đủ tại `.agents/skills/frontend-design/LICENSE.md`.
- Phạm vi: theo description của skill và ownership; skill không mở rộng quyền của agent.
- Phụ thuộc: chỉ instructions được định tuyến trong AGENTS.md; không tự cài skill hoặc tool khác.

## Skill tự viết của Claude Code
Năm skill dưới đây do Claude Code tự viết trong CLA-GOV-001 ngày 29/10/2026, không có repository nguồn ngoài.
- Agent: claude.
- License: nội bộ dự án Smart Healthcare; repository chưa chọn license công khai.
- Phạm vi: theo description của skill, claude-scope.md và ownership; skill không mở rộng quyền.
- Phụ thuộc: chỉ instructions trong docs/ai/instructions/; không script, hook hay cơ chế tự gọi mạng.

| Skill | Đường dẫn dùng | Nguồn |
|---|---|---|
| chatbot-add-tool | `.claude/skills/chatbot-add-tool/SKILL.md` | Tự viết |
| chatbot-finetune-eval | `.claude/skills/chatbot-finetune-eval/SKILL.md` | Tự viết |
| chatbot-safety-check | `.claude/skills/chatbot-safety-check/SKILL.md` | Tự viết |
| cloudflare-access-tunnel | `.claude/skills/cloudflare-access-tunnel/SKILL.md` | Tự viết |
| handoff | `.claude/skills/handoff/SKILL.md` | Tự viết |
