# Design System — Staff Web (`frontend/web`)

> Áp dụng cho toàn bộ cổng nội bộ nhân viên (`Receptionist`, `Nurse`, `Doctor`, `RoomAccount`, `Admin`), dùng chung nhận diện thương hiệu Emerald (`#006a41`) của hệ thống nhưng tối ưu cho **mật độ thông tin cao, thao tác nhiều giờ liên tục, bàn phím nhanh, chính xác tuyệt đối**.

---

## 1. Nguyên tắc thiết kế cốt lõi

1. **Rõ vai trò, ranh giới cứng**:
   - **Admin** chỉ quản trị cấu hình, danh mục, duyệt tài chính/ngoại lệ, kiểm toán và báo cáo — **tuyệt đối không** có nút thao tác lâm sàng (khám, kê đơn, nhập sinh hiệu/CLS) hoặc nghiệp vụ quầy (check-in, thu tiền, đánh dấu đã trả).
   - **Bác sĩ** làm việc trên bàn khám tách rõ 2 hàng đợi (Khám đầu / Quay lại) và chỉ có nút **"Gọi người tiếp theo"** theo hàng đợi hệ thống.
   - **Phòng CLS (`RoomAccount`)** làm việc theo phiên phòng/máy; khi bị mở phiên nơi khác (`409` / SignalR session replaced) phải dừng thao tác ngay và điều hướng sang màn thông báo thay thế phiên (`S04b`).
2. **Độ tương phản lâm sàng & chống mỏi mắt**: Nền làm việc chính `#f4f9f6` / `#ffffff`, nhấn điểm emerald `#006a41` có kiểm soát, badge trạng thái rõ màu kèm văn bản (không dựa vào màu đơn thuần).
3. **Chống mất dữ liệu & ghi đè thầm lặng**: Mọi thao tác cập nhật có `version` (`If-Match` / `expectedVersion`) đều bắt `409 Conflict` qua `VersionConflictDialog` dùng chung — **không bao giờ tự động ghi đè**.

---

## 2. Tokens màu sắc & mật độ

| Token CSS | Giá trị | Vai trò sử dụng |
| --- | --- | --- |
| `--color-primary` | `#006a41` | Hành động chính, active nav |
| `--color-primary-container` | `#028654` | Nút nổi bật, pill vai trò đang hoạt động |
| `--color-surface` | `#f4f8f6` | Nền workspace staff (trung tính hơn patient để bảng dữ liệu dễ đọc 8 giờ/ngày) |
| `--color-surface-card` | `#ffffff` | Thẻ nội dung, bảng, khu vực nhập liệu |
| `--color-surface-muted` | `#e9f2ed` | Header bảng, panel phụ |
| `--color-on-surface` | `#0f1f18` | Văn bản chính |
| `--color-on-surface-variant` | `#44544c` | Nhãn cột, mô tả phụ |
| `--color-border` | `#cddbd3` | Viền bảng, đường phân vùng split-pane |
| `--color-info` | `#0b57d0` / `#e8f0fe` | Trạng thái thông tin, Đã gọi (`Called`) |
| `--color-warning` | `#9a5b00` / `#fef3c7` | Trì hoãn (`Deferred`), Chờ kết quả (`Pending`), Quá ngưỡng sinh lý |
| `--color-error` | `#ba1a1a` / `#ffdad6` | Cảnh báo lỗi, quá hạn, xung đột `409` |
| `--color-success` | `#006a41` / `#d4f7e2` | Hoàn tất, Đã phát hành (`Published`), Khớp đối soát |

---

## 3. Typography & Bảng dữ liệu

- Font: `'Be Vietnam Pro', sans-serif`, mặc định `14px` cho toàn bộ biểu mẫu và bảng nghiệp vụ, `12px–13px` cho badge và meta cột phụ.
- Số tiền (`VNĐ`), CCCD che bớt (`*********123`), mã hóa đơn, số đo sinh hiệu, timestamp luôn dùng `font-variant-numeric: tabular-nums`.
- Bảng chuẩn: header dính (`sticky top-0`), chiều cao hàng `44px` (`compact 38px` cho hàng chờ đông), căn phải cho cột tiền/số lượng, căn trái cho văn bản.

---

## 4. Shell & Điều hướng (`StaffLayout`)

- **Sidebar trái (`240px`)**: Gom nhóm theo vai trò đang đăng nhập từ `menus.ts`, có thể thu gọn trên tablet/nhỏ hơn.
- **Top bar (`56px`)**: Tiêu đề phân hệ, thông tin nhân sự/phòng hiện tại, **Chuông thông báo realtime (`S03`)**, badge trạng thái kết nối SignalR (hiện banner cảnh báo nếu mất kết nối), và nút Đăng xuất (`POST /auth/room/logout` với `RoomAccount`).
- **Toast & Banner realtime**: Cập nhật SignalR đi qua `aria-live="polite"`; xung đột `409`, mất kết nối hoặc phiên bị đá (`S04b`) dùng dialog/banner rõ hành động tiếp theo.

---

## 5. Bảo mật hiển thị (PII & Audit)

- Ở AuditLog (`AD15`), CCCD và số điện thoại bắt buộc hiển thị dạng đã che, chỉ giữ **3 ký tự cuối** (ví dụ `*********789` / `*******321`).
- Không lộ link R2 presigned hay stack trace kỹ thuật lên UI; mọi lỗi `ProblemDetails.code` được ánh xạ sang thông điệp tiếng Việt thân thiện kèm mã tham chiếu khi cần hỗ trợ kỹ thuật.
