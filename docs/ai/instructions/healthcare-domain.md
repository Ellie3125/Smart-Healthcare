# healthcare-domain

## Phạm vi
Nghiệp vụ, API, UI hiển thị trạng thái; toàn bộ dữ liệu demo là giả lập.

## Khi nào đọc
Khi thiết kế tính năng, contract, validation hoặc luồng hiển thị.

## Quy tắc
- **MED-01** — Chatbot và mọi thành phần AI không chẩn đoán, chọn thuốc, kê đơn, đề xuất CLS hoặc quyết định thứ tự khám. Gợi ý chuyên môn bằng AI và dự đoán bỏ hẹn nằm ngoài phạm vi; bác sĩ quyết định qua giao diện nghiệp vụ. Xem chatbot-langchain-ollama.md.
- **MED-02** — Bác sĩ quyết định chỉ định/kết luận. Kỹ thuật viên cập nhật thực hiện/kết quả theo quyền; kết quả chỉ hiển thị cho bệnh nhân khi được phát hành.
- **MED-03** — PatientId nội bộ khác CCCD; hồ sơ có thể chưa có CCCD. CCCD không là mật khẩu, khóa chính, object key hoặc dữ liệu gửi cho chatbot/model. QR chỉ là mã tham chiếu có hạn/quyền kiểm soát.
- **MED-04** — Tách state machine Appointment, Payment, Encounter, ServiceOrder, ServiceResult. Mỗi transition chỉ rõ actor, guard, side effect và audit. Không gộp vào một status.
- **BR-001** — Giữ chỗ có hạn, server quản lý sức chứa và thời gian; cạnh tranh suất cuối không được vượt capacity. Price snapshot từ server, không tin giá do client gửi.
- **BR-002** — Phí khám và phí CLS thanh toán qua VNPT/MoMo theo phương thức đã cấu hình. Chỉ ghi Paid sau khi server xác minh đúng giao dịch, chữ ký, số tiền, người nhận và mã tham chiếu; callback/IPN lặp phải idempotent; sự kiện Pending/Failed cũ không ghi đè Paid đã xác minh. Return URL/màn hình bệnh nhân không đủ để xác nhận. Sản phẩm VNPT/MoMo và merchant còn mở theo Phạm vi v2.1 §7.
- **BR-003** — Thiết kế nhánh thanh toán đến muộn, callback sau hết hold, khách đóng trình duyệt, hủy/đổi trạng thái. Không tự hồi sinh suất đã cấp cho người khác; trạng thái chờ đối soát cần được chốt trong contract.
- **SEC-01** — Backend kiểm cả vai trò và quyền trên đúng hồ sơ/lượt khám; bác sĩ/admin không mặc nhiên đọc mọi bệnh án. Audit xem/sửa/duyệt/phát hành với dữ liệu tối thiểu.
- **SEC-02** — Không commit secret hoặc bệnh án thật; không log token, CCCD, triệu chứng chi tiết, kết quả hay presigned URL. Không dùng dữ liệu thật trong demo công khai.

## Điều cấm
Không gọi hệ thống là công cụ chẩn đoán, không biến điểm mô hình thành xác suất mắc bệnh. Không tuyên bố đạt điều kiện vận hành y tế thực tế.

## Cách kiểm chứng
Test A không xem B; CCCD đúng vẫn thiếu quyền; bác sĩ không được phân công bị từ chối; kết quả draft không thấy ở bệnh nhân; callback lặp/muộn/sai chữ ký/sai số tiền; hai backend tranh suất cuối.

## Nguồn
Phạm vi nghiệp vụ v2.1 §2, §5.5, §6; Giải pháp kỹ thuật v2.2 §5, §11. Master phần I §2–4,7–8,11,13; đây là Project requirements, không gán cho PDF học phần.
