---
name: chatbot-add-tool
description: "Dùng khi thêm hoặc sửa một công cụ LangChain của chatbot gọi REST API .NET (tìm lịch, đặt/đổi/hủy, tra vé, xem thanh toán khám). Không dùng khi chưa có contract Approved."
---

# Thêm công cụ chatbot

1. Đọc chatbot-langchain-ollama.md (BOT-03..06) và api-contract-and-handoff.md. Tìm contract của endpoint trong docs/api/ và xác nhận status Approved.
2. Chưa có contract Approved hoặc thiếu trường cần thiết: dừng code, dùng skill `handoff` gửi claude-to-codex. Không đoán endpoint hay schema.
3. Xác nhận công cụ thuộc whitelist BOT-01; công cụ thay đổi lịch chỉ được tạo/đọc đề nghị do .NET lưu, không tự thực thi theo `confirmed` của model.
4. Viết công cụ trong ml-service/: tham số model điền chỉ gồm dữ liệu nghiệp vụ (bác sĩ, ngày, mã đề nghị…), danh tính lấy từ phiên .NET; gọi API bằng phiên người dùng, có timeout; không mở SQL, shell hay HTTP tùy ý.
5. Map lỗi API (401/403/404/409/429/timeout) thành phản hồi để model trình bày lại; không báo thành công khi API chưa trả thành công.
6. Viết test trong tests/ml/ với API giả theo contract.

## Checklist kiểm chứng
- [ ] Test sai quyền: hỏi dữ liệu người khác → API 403, không lộ dữ liệu.
- [ ] Test hết chỗ: API trả hết suất → chatbot báo hết, giữ thông tin đã nhập.
- [ ] Test timeout API và timeout model → không thay đổi gì, gợi ý làm thủ công trên cùng BookingAttempt.
- [ ] Không có tham số UserId/PatientId do model điền.
- [ ] `pytest` và `ruff` chạy thật, ghi command/exit code vào session.
