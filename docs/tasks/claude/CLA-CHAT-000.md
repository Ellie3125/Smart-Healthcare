# CLA-CHAT-000 — Khung thư mục chatbot

- task_id: CLA-CHAT-000
- owner_agent: claude
- owner_member: Phạm Hoà (Ellie3125, trưởng nhóm)
- base_commit: cd7da64 (sau CLA-GOV-001)
- branch: ellie3125/claude-governance
- requirement_ids: BOT-01..10; Giải pháp kỹ thuật v2.1 §8.7, §11
- allowed_paths: ml-service/**, tests/ml/**, docs/tasks/claude/**
- contract_version: không áp dụng (chưa có code gọi API)
- acceptance_criteria: cây ml-service khớp BOT-04/08/09 (app, app/tools, training/data/{train,validation,test}, ollama), tests/ml/safety cho bộ đối kháng; bỏ notebooks/ và artifacts/ của hướng ML cũ; README mô tả cây; không đụng backend/frontend/compose.
- status: In review
- depends_on: CLA-GOV-001
- next_action: Chốt phiên bản Python, FastAPI, LangChain, image Ollama và model nền (Qwen2.5 3B đề xuất) rồi tạo pyproject; chờ contract API từ Codex để làm công cụ đầu tiên.

Trưởng nhóm chọn trong chat: chỉ dựng phần thuộc Claude Code; Backend vẫn do Codex làm theo docs/handoffs/claude-to-codex/CLA-GOV-001.md.
