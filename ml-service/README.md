# ml-service — chatbot Hướng 2

Dịch vụ chatbot của bệnh nhân: FastAPI + LangChain `create_agent`, gọi model qua Ollama nội bộ. Owner: Claude Code. Luật: docs/ai/instructions/chatbot-langchain-ollama.md (BOT-01..10).

| Thư mục | Nội dung |
|---|---|
| `app/` | FastAPI: nhận hội thoại từ phiên .NET, khai báo agent, checkpointer theo ConversationId |
| `app/tools/` | Công cụ trong whitelist, mỗi công cụ gọi một REST API .NET theo contract Approved (skill chatbot-add-tool) |
| `training/` | Script fine-tune và đánh giá model nền/sau fine-tune (skill chatbot-finetune-eval) |
| `training/data/{train,validation,test}/` | Hội thoại giả lập, tách theo kịch bản trước khi huấn luyện |
| `ollama/` | Modelfile và tag model đã ghim |
| `../tests/ml/` | Test công cụ với API giả; `safety/` chứa bộ hội thoại đối kháng (skill chatbot-safety-check) |

Không commit trọng số model, adapter hay GGUF. Lưu chúng ngoài git và ghi tag, checksum cùng nơi lưu vào docs/evidence/claude/.

Service `chatbot` và `ollama` trong compose thuộc Codex. Chưa có pyproject/dependency vì phiên bản FastAPI, LangChain và Ollama chưa được chốt.
