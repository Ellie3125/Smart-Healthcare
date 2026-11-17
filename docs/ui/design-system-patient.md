# Design System — Patient Web (`frontend/patient-web`)

> Áp dụng cho toàn bộ `frontend/patient-web`, kế thừa hệ màu và typography từ `legacy-ui/*.html` (`Be Vietnam Pro`, Material 3 Emerald Healthcare), tối ưu cho trải nghiệm bệnh nhân đa lứa tuổi trên di động và desktop.

---

## 1. Nguyên tắc thiết kế cốt lõi

1. **Ấm áp, dễ hiểu, giảm lo âu**: Không gian thoáng, ngôn ngữ tiếng Việt tự nhiên, không thuật ngữ kỹ thuật/nội bộ.
2. **Tách biệt hoàn toàn với Staff Web**: Không import, không link, không xuất hiện chuỗi `/admin`, `/doctor`, `/reception`, `/room`, `/nurse`, `staff` trong bundle.
3. **Di động trước, một tay dễ bấm**: Nút/ô nhập tối thiểu `44×44px` trên mobile; form tuần tự rõ bước; không cắt xén chữ tiếng Việt.
4. **Dữ liệu nhạy cảm & tiền tệ rõ ràng**:
   - Tiền tệ dùng `font-variant-numeric: tabular-nums` với đơn vị `₫` / `VNĐ`.
   - Thời gian hiển thị theo múi giờ Việt Nam (`Asia/Ho_Chi_Minh`, `UTC+7`).
   - Không render URL lưu trữ file trực tiếp (`r2.cloudflarestorage`); file CLS tải/xem qua Backend stream/blob.

---

## 2. Tokens màu sắc

| Token CSS | Giá trị Hex | Vai trò sử dụng |
| --- | --- | --- |
| `--color-primary` | `#006a41` | CTA chính, liên kết nổi bật, trạng thái đang chọn (tỷ lệ tương phản trên trắng > 6.4:1, đạt WCAG AA/AAA) |
| `--color-primary-container` | `#028654` | Nút nhấn nổi, badge nhấn mạnh, gradient nhẹ ở thẻ vé/bước thanh toán |
| `--color-on-primary` | `#ffffff` | Chữ/icon trên nền `--color-primary` |
| `--color-primary-fixed` | `#8ef8bb` | Nền nhấn nhẹ cho badge thông tin tích cực |
| `--color-secondary` | `#076c43` | Nhãn phụ, nút phụ xác nhận |
| `--color-secondary-container` | `#9ef5c0` | Pill trạng thái thành công / đã xác nhận |
| `--color-on-secondary-container` | `#005231` | Chữ trên nền `--color-secondary-container` |
| `--color-surface` | `#ebfef2` | Nền tổng thể nhẹ dịu mắt (Patient emerald tint) |
| `--color-surface-container-lowest` | `#ffffff` | Card nội dung chính, dialog, sheet |
| `--color-surface-container-low` | `#e5f8ed` | Card phụ, khung tóm tắt |
| `--color-surface-container` | `#e0f2e7` | Vùng phân nhóm nhẹ, skeleton |
| `--color-surface-container-high` | `#daede1` | Vùng hover nhẹ, chip không chọn |
| `--color-on-surface` | `#0f1f18` | Văn bản chính |
| `--color-on-surface-variant` | `#3e4941` | Văn bản phụ, nhãn mô tả |
| `--color-outline` | `#6e7a71` | Viền focus/nhãn phụ |
| `--color-outline-variant` | `#bdcabf` | Viền thẻ mặc định, đường phân cách |
| `--color-warning` | `#9a5b00` | Cảnh báo nghiệp vụ (cutoff 05:00, hết hạn 5p/15p, Pending) |
| `--color-warning-container` | `#fef3c7` | Nền cảnh báo amber |
| `--color-error` | `#ba1a1a` | Lỗi form, hủy, từ chối |
| `--color-error-container` | `#ffdad6` | Nền báo lỗi |
| `--color-on-error-container` | `#93000a` | Chữ trên nền báo lỗi |
| `--color-tertiary` | `#b6171e` | Cảnh báo khẩn cấp y khoa (115 Red-flag) |

---

## 3. Typography (`Be Vietnam Pro`)

- Font chính: `'Be Vietnam Pro', system-ui, -apple-system, sans-serif` (cùng font chữ số bảng `tabular-nums` cho tiền tệ, giờ, số vé).
- **Scale chuẩn**:
  - `display`: `32px–36px` / `40px–44px`, weight `600`, tracking `-0.02em` (Số vé TV01 dùng cỡ lớn hơn `clamp(2.5rem, 6vw, 4.5rem)`).
  - `headline-lg`: `24px–28px` / `32px–36px`, weight `600`.
  - `headline-md`: `20px` / `28px`, weight `600`.
  - `headline-sm`: `18px` / `24px`, weight `600`.
  - `body-lg`: `16px` / `24px`, weight `400` (nội dung đọc chính trên mobile/bệnh nhân).
  - `body-md`: `14px` / `20px`, weight `400`.
  - `label-md`: `14px` / `20px`, weight `500`.
  - `label-sm`: `12px` / `16px`, weight `500`.

---

## 4. Layout & Điều hướng

- **Desktop & Tablet**: Top header (`max-w-6xl mx-auto`), nút thông báo (chuông + badge chưa đọc), menu hồ sơ/tài khoản, link chính: *Hồ sơ*, *Đặt lịch*, *Lịch của tôi*, *Hành trình hôm nay*, *Kết quả & Đơn thuốc*, *Thanh toán & Hoàn tiền*, *Trợ lý ảo*.
- **Mobile**: Thanh điều hướng dưới đáy (`bottom nav` cố định an toàn `env(safe-area-inset-bottom)`) cho 4–5 luồng chính + nút nổi Chatbot (`P25`).
- **Màn hình công khai TV01 (`/display`)**: Shell riêng tối giản không cần đăng nhập, không header người dùng, độ tương phản cao đọc từ xa ≥ 3m, chỉ hiển thị số thứ tự và phòng/quầy, không hiển thị bất kỳ thông tin định danh cá nhân (tên, ngày sinh, SĐT, CCCD).

---

## 5. Quy tắc Component & Trợ năng (WCAG AA)

- Mọi `<input>`, `<select>`, `<textarea>` bắt buộc liên kết `htmlFor` + `id` và `aria-describedby` khi có lỗi.
- Không dùng emoji làm biểu tượng giao diện — chỉ dùng `lucide-react` với `aria-hidden="true"` khi đi kèm nhãn văn bản, hoặc `aria-label` khi là nút icon.
- Cập nhật realtime (SignalR ở `P16`, đếm ngược giữ chỗ ở `P08`, đếm ngược CLS `P17`, trạng thái Return URL `P10`) gắn `aria-live="polite"` (hoặc `"assertive"` với cảnh báo lỗi nghiêm trọng).
- Tuân thủ `@media (prefers-reduced-motion: reduce)`: tắt hiệu ứng nảy/pulse không cần thiết.
