---
name: frontend-design
description: "Thiết kế hoặc sửa UI React/Vite/TypeScript Smart Healthcare, trạng thái tương tác và accessibility; chỉ Antigravity được sửa UI."
license: "Complete terms in LICENSE.md"
---

# Thiết kế Frontend Smart Healthcare

1. Đọc scope Antigravity, frontend-react.md, healthcare-domain.md và api-contract-and-handoff.md. Đọc use case/contract Approved; thiếu API thì handoff Codex, chưa đoán endpoint/schema hoặc sửa server.
2. Xác định actor, tác vụ chính và thông tin cần thấy. Viết luồng màn hình và design tokens thống nhất với UI hiện có: font đọc được tiếng Việt, màu/spacing/type scale nhất quán, trạng thái rõ. Không giả yêu cầu hoặc lịch sử thẩm mỹ của khách hàng.
3. Chọn bố cục theo tác vụ khám/đặt lịch/thực hiện dịch vụ, ưu tiên dễ đọc và an toàn thao tác. AI gợi ý, chỉ định bác sĩ và kết quả phát hành phải phân biệt rõ; không mô tả điểm model là xác suất mắc bệnh.
4. Implement trong frontend/**, component tests và tests/e2e/** đúng ownership. Giữ React/Vite/TypeScript, không thêm Next.js/hosting/CDN/font/analytics ngoài yêu cầu. Không sửa Backend/DB/ML/pipeline.
5. Bao phủ loading/empty/error/validation/permission/timeout/conflict; API client theo contract, mock có nhãn và tách chế độ thật. Không optimistic-confirm thanh toán/slot hoặc tự đổi phí/quyền/trạng thái server.
6. Bàn phím/focus/label/error message/contrast/responsive/reduced motion; màu không là dấu hiệu duy nhất. Dùng ngôn ngữ tiếng Việt rõ thao tác, tránh trang trí che thông tin y tế.
7. Giữ React state tối thiểu; derive dữ liệu tính được, cleanup request/effect và functional update khi dựa state trước. Tối ưu khi có đo đạc, không thêm dependency chỉ để theo mẫu.
8. Kiểm giao diện trên viewport phù hợp, tương tác và lỗi quan trọng. Browser/E2E dùng locator assertion chờ điều kiện; screenshot phải là UI thật. Báo riêng UI-mock, UI API thật và E2E; cập nhật task/evidence/handoff.

Codex có thể đọc skill để hiểu/review nhưng không được dùng skill này để tạo/sửa/xóa/format UI.

Nguồn: https://github.com/anthropics/skills/tree/dbd4588f9e1033efb41dad4bef2f7947c8993d44/skills/frontend-design. Bản hướng dẫn đã được biên tập cho Smart Healthcare, ngày 16/10/2026; giữ license tác giả trong LICENSE.md.
