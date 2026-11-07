# chatbot-service — chatbot Hướng 2

Dịch vụ chatbot của bệnh nhân: FastAPI + LangChain `create_agent`, gọi model qua Ollama nội bộ. Owner: Claude Code. Luật: docs/ai/instructions/chatbot-langchain-ollama.md (BOT-01..12).

| Thư mục | Nội dung |
|---|---|
| `app/` | FastAPI: nhận hội thoại từ phiên .NET, khai báo agent với Data/State/Context Schema và Short-term Memory (checkpointer theo ConversationId), theo Giải pháp kỹ thuật v2.11 §11.3–11.4 |
| `app/tools/` | Công cụ trong whitelist, mỗi công cụ gọi một REST API .NET theo contract Approved (skill chatbot-add-tool) |
| `ollama/` | Modelfile và tag model đã ghim |
| `tests/tools/` | Test từng tool của agent với API giả theo contract (skill chatbot-add-tool) |
| `tests/scenarios/` | Kịch bản hội thoại chuẩn theo intent (skill chatbot-finetune-eval) |
| `tests/safety/` | Hội thoại đối kháng, câu hỏi ngoài phạm vi (skill chatbot-safety-check) |

Không commit trọng số model, adapter hay GGUF. Lưu chúng ngoài git và ghi tag, checksum cùng nơi lưu vào docs/evidence/claude/.

Service `chatbot` và `ollama` trong compose thuộc Codex. Chưa có pyproject/dependency vì phiên bản FastAPI, LangChain và Ollama chưa được chốt.
