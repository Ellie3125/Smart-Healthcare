---
name: chatbot-finetune-eval
description: "Dùng khi chuẩn bị dữ liệu hội thoại, fine-tune model nền, đánh giá trước/sau, xuất Modelfile Ollama hoặc đổi tag model của chatbot."
---

# Fine-tune và đánh giá chatbot

1. Đọc chatbot-langchain-ollama.md (BOT-08, BOT-09) và healthcare-domain.md (SEC-02). Ghi task với model nền, phiên bản dữ liệu và tiêu chí đạt.
2. Dữ liệu: hội thoại giả lập trong ml-service/, có đầu ra chuẩn; bao gồm thiếu thông tin, hết chỗ, quá hạn 05:00/09:30, timeout, sai quyền và yêu cầu chuyên môn phải từ chối. Không dữ liệu bệnh nhân thật.
3. Tách train/validation/test theo kịch bản trước khi huấn luyện; ghi checksum từng tập. Không chỉnh dữ liệu test sau khi xem kết quả.
4. Chạy đánh giá model nền trên tập test làm baseline.
5. Fine-tune (adapter hoặc tham số); version hóa model nền, cấu hình, seed, adapter/checkpoint.
6. Đánh giá model sau fine-tune trên cùng tập test và cùng cách chấm; đo thời gian phản hồi trên phần cứng ghi rõ.
7. Xuất Modelfile (GGUF hoặc adapter), ghim tag model và phiên bản image Ollama; không dùng latest.
8. Chạy skill `chatbot-safety-check` trên model mới trước khi đề xuất bật.

## Checklist kiểm chứng
- [ ] Evidence thật trong docs/evidence/claude/: lệnh, cấu hình, checksum dữ liệu, kết quả trước/sau, phần cứng.
- [ ] Không bịa hoặc làm tròn đẹp metrics; tầng chưa chạy ghi Not run.
- [ ] Tag model và image đã ghim, Modelfile có trong repo.
- [ ] Chỉ tải model hoặc sửa prompt thì không ghi là đã fine-tune.
