# chatbot-langchain-ollama

## Phạm vi
Claude Code: ml-service/** (dịch vụ chatbot FastAPI/LangChain, Modelfile Ollama, dữ liệu và script fine-tune/đánh giá), tests/ml/** và docs/ml/**. Service `chatbot` và `ollama` trong docker-compose.yml do Codex sửa theo handoff.

## Khi nào đọc
Thiết kế hoặc sửa chatbot, thêm công cụ, chuẩn bị dữ liệu hội thoại, fine-tune, đánh giá, viết Modelfile hoặc đóng gói chatbot.

## Quy tắc
- **BOT-01** — Chatbot chỉ phục vụ bệnh nhân/người đại diện: tìm lịch, chuẩn bị đặt/đổi/hủy, xem lịch của tôi, tra vé và xem trạng thái thanh toán khám của chính người dùng theo quyền. Chatbot triển khai sau khi API lịch, vé và thanh toán ổn định; khi chưa bật hoặc lỗi, mọi chức năng vẫn dùng được qua giao diện thủ công.
- **BOT-02** — Không chẩn đoán, không chọn thuốc hay kê đơn, không đề xuất CLS, không gợi ý chuyên khoa từ triệu chứng, không quyết định thứ tự khám. Gặp yêu cầu chuyên môn thì từ chối và hướng tới nhân viên/bác sĩ; không đi vòng giới hạn đăng ký.
- **BOT-03** — FastAPI chỉ nhận hội thoại từ phiên .NET đã xác thực. UserId/PatientId lấy từ phiên do .NET truyền, không là tham số model tự điền. Ngữ cảnh và lịch sử tách theo tài khoản và ConversationId.
- **BOT-04** — Chỉ khai báo công cụ trong whitelist cho `create_agent`; mỗi công cụ gọi REST API công khai của .NET theo contract Approved trong docs/api bằng phiên người dùng, giống app. Không gọi trực tiếp contract module hay DB; không có công cụ chạy SQL, shell hoặc HTTP tùy ý.
- **BOT-05** — Thay đổi lịch chỉ thực thi qua đề nghị do .NET lưu, gắn người dùng, nội dung và thời hạn; trường `confirmed` hay câu đồng ý do model sinh không đủ để thực thi. Khi xác nhận, Backend kiểm lại quyền, version, thời hạn, sức chứa và idempotency trên primary. Thành công chỉ báo theo phản hồi API.
- **BOT-06** — Không giữ transaction DB khi chờ model hoặc người dùng. Model lỗi, timeout hoặc trả tham số sai không thay đổi DB; người dùng tiếp tục thủ công trên cùng BookingAttempt. Checkpoint hội thoại không là bằng chứng có suất hay đã thanh toán. Lời gọi model và API có timeout giới hạn.
- **BOT-07** — FastAPI + LangChain `create_agent`, lịch sử theo ConversationId bằng checkpointer của LangChain. Không tự dựng graph LangGraph, không dùng Unified Model Adapter hay lớp adapter đa nhà cung cấp.
- **BOT-08** — Ollama chạy container riêng, chỉ mở trong mạng Docker nội bộ, không publish port. Ghim tag model và phiên bản image (không latest); dùng cùng cấu hình ở laptop và VPS. Không gửi dữ liệu bệnh nhân ra API model bên ngoài, kể cả khi model nội bộ lỗi. Model nền (đề xuất Qwen2.5 3B), tốc độ phản hồi và giới hạn tài nguyên phải đo trên VPS thực trước khi chốt.
- **BOT-09** — Fine-tune: version hóa dữ liệu, model nền, cấu hình huấn luyện, adapter/checkpoint, Modelfile và kết quả đánh giá. Dữ liệu hội thoại giả lập, có trường hợp thiếu thông tin, hết chỗ, quá hạn, timeout và sai quyền. Tách train/validation/test theo kịch bản trước huấn luyện; so sánh model nền và model sau fine-tune trên cùng tập test. Không bịa metrics; chỉ tải model hoặc sửa prompt chưa là fine-tune. Lịch và trạng thái thay đổi lấy qua API, không huấn luyện vào trọng số.
- **BOT-10** — Không log token, CCCD, nội dung hội thoại chi tiết hay dữ liệu cá nhân. Bộ hội thoại đối kháng (xin chẩn đoán, hỏi dữ liệu người khác, ép xác nhận, prompt injection) phải đạt trước khi bật profile `chatbot`: không lộ dữ liệu, không thay đổi gì khi thiếu quyền.

## Điều cấm
Không public chatbot hay Ollama qua Nginx/Cloudflare; client chỉ chat qua .NET. Không tải model, adapter hoặc pickle không rõ nguồn/license. Không mô tả chatbot là tư vấn y khoa. Không dùng dữ liệu bệnh nhân thật để huấn luyện hoặc đánh giá.

## Cách kiểm chứng
pytest và ruff cho ml-service khi có code; test công cụ với API giả theo contract cho sai quyền, hết chỗ và timeout; bộ đối kháng theo skill chatbot-safety-check; đánh giá trước/sau fine-tune theo skill chatbot-finetune-eval với evidence thật trong docs/evidence/claude/; kiểm Ollama không có cổng host.

## Nguồn
Phạm vi nghiệp vụ v2.1 §1, §2.1–2.2, §7; Giải pháp kỹ thuật v2.1 §2.1, §7, §8.7, §11, §13.1 bước 7, §13.2, §14.
