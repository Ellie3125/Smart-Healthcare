# Mapping `legacy-ui/*.html` → Smart Healthcare UI Screens

> Tài liệu này đối chiếu 12 file prototype trong `legacy-ui/` với đặc tả nghiệp vụ `Smart-Healthcare-Prompt-Vibe-Code-UI.md`, `erd-v2.4-data-dictionary.md`, `docs/conventions.md` và `docs/api/*.md`.
>
> **Quy ước trạng thái**:
> - **Đã có (giữ nguyên)**: Màn hình đã được cài đặt bằng React + TypeScript trước đợt này — **không viết lại**, chỉ tích hợp điều hướng/test khi cần.
> - **Cần tạo mới / Nâng cấp stub**: Màn hình chưa có hoặc mới chỉ là stub tĩnh tuần 1.

---

## 1. Bảng ánh xạ từ `legacy-ui/*.html`

| File legacy | Shell HTML | Màn hình mục tiêu | Trạng thái hiện tại | Ghi chú chuẩn hóa theo nghiệp vụ v2.4 & Backend thật |
| --- | --- | --- | --- | --- |
| `legacy-ui/dang-nhap.html` | `web_blank` | **P01–P02** (`frontend/patient-web`), **S01** (`frontend/web`) | **Đã có** (`LoginPage.tsx`, `RegisterPage.tsx`) | Giữ nguyên trang đã cài đặt; bổ sung kiểm thử component & error code i18n. |
| `legacy-ui/chon-bac-si.html` | `web_standard` | **P06–P07** (Tìm bác sĩ & Slot theo ngày) | **Cần tạo mới** | Nối API thật `GET /api/scheduling/patient/doctors`. Hiển thị theo mô hình `DoctorDay` (30 slot công bằng: giữ chỗ online + walk-in chung quota, không chọn giờ cứng nếu backend chỉ trả slot tổng/đếm). |
| `legacy-ui/xacnhan-thanhtoan.html` | `web_standard` | **P08–P11** (Xác nhận + Hold 5p, VNPAY Checkout, Return URL 6 trạng thái, Recovery) | **Cần tạo mới** | Nối `POST /api/scheduling/patient/booking-attempts/{id}/hold` + `Idempotency-Key`. Countdown 5 phút. Return URL bắt đầu bằng trạng thái **"Đang xác minh"** (polling/SSE/SignalR), **tuyệt đối không** chốt "Thành công" trước khi API xác nhận và **không** có nút "Đánh dấu đã trả". |
| `legacy-ui/lich-cua-toi.html` | `web_standard` | **P12–P15** (Lịch của tôi, Vé QR, Đổi lịch, Hủy lịch) | **Cần tạo mới** | Đổi lịch chỉ cho phép **cùng mức giá** và **trước 05:00** ngày khám; Hủy lịch luôn hiển thị `refundAmount` tính từ server trước khi xác nhận + khóa double-click bằng `Idempotency-Key`. |
| `legacy-ui/ve-kham.html` | `web_standard` | **P13** (Vé khám QR), **P16** (Hành trình hôm nay), **TV01** (`/display`) | **Cần tạo mới** | Vé chỉ cấp số thứ tự sau Check-in; **TV01 (`/display`)** là màn công khai tách riêng chỉ hiện **số vé + phòng/quầy**, **tuyệt đối không có PII bệnh nhân**. |
| `legacy-ui/ho-so-kham.html` | `web_standard` | **P17–P23** (Thanh toán CLS, Chi phí, Hoàn tiền, Lịch sử hồ sơ, Chi tiết, CLS file, Đơn thuốc) | **Cần tạo mới** | **P20** chỉ hiển thị hồ sơ đã `Verified` (`Pending` → P26). **P21** ẩn nội dung chưa `Published`. **P22** tải file CLS qua stream/blob của Backend, **không lộ URL R2**. **P23** chỉ đọc, không nút mua/đặt thuốc hay tồn kho. |
| `legacy-ui/chatbot.html` | `web_standard` | **P25** (Chatbot nổi + `/chatbot`) | **Cần tạo mới** | Tôn trọng `/health` (`available=false` → banner bảo trì), **cảnh báo 115** khi có red-flag (không gắn nút đặt lịch), thẻ đề xuất chuyên khoa có disclaimer, thẻ xác nhận phải gọi API Backend. |
| `legacy-ui/tiep-tan.html` | `web_dashboard` | **RC01–RC10** (`frontend/web`) | **RC05 Đã có** (Tab Xác minh trong `ReceptionPage.tsx`); Tab 2 là stub tuần 1 (`mockTickets`); **RC01–RC04, RC06–RC10 Cần tạo/nâng cấp** | Giữ nguyên Tab 1 Xác minh CCCD/hồ sơ (`RC05`). Thay Tab 2 placeholder và mở rộng đầy đủ RC01–RC10: Walk-in dùng chung 30 slot, Check-in cần xác nhận giấy tờ + chặn quá `CheckInDeadline`, Thu ngân không sửa số tiền, `querydr`, Hủy `CheckedIn` bắt buộc lý do, Hoàn tiền mặt, Chốt ca, DS cần gọi. |
| `legacy-ui/do-sinh-hieu.html` | `web_dashboard` | **NU01–NU02** (`frontend/web`) | **Stub tĩnh** (`NursePage.tsx` 35 dòng) | Nâng cấp thành hàng chờ điều dưỡng (`/nurse/queue`) + form sinh hiệu (`/nurse`): cảnh báo ô trống trước khi gửi, cảnh báo vượt ngưỡng sinh lý, xử lý `409 Conflict` (`version`). |
| `legacy-ui/ban-lam-viec-bac-si.html` | `web_dashboard` | **DR01–DR10** (`frontend/web`) | **Stub tĩnh** (`DoctorPage.tsx` 35 dòng) | Nâng cấp thành bàn làm việc bác sĩ đầy đủ: 2 hàng đợi (Khám đầu / Quay lại), **chỉ có nút "Gọi người tiếp theo"** do Backend chọn (không tự chọn bệnh nhân bất kỳ), Auto-draft, ICD-10, Chỉ định CLS theo phòng đang hoạt động, Phát hành + Bổ sung có diff, Kê đơn Full-Text (không tồn kho), `VersionConflictDialog`. |
| `legacy-ui/quan-tri-lich-kham.html` | `web_dashboard` | **AD01–AD17** (`frontend/web`) | **AD01–AD05 Đã có** (`StaffTab`, `DoctorsTab`, `SpecialtiesTab`, `DoctorRanksTab`, `RoomsTab`, `ShiftsTab`); **AD03 ServiceStation + AD06–AD17 Cần tạo mới** | Giữ nguyên toàn bộ tab AD01–AD05 hiện tại. Bổ sung Quản lý Quầy (`ServiceStation`) + Cấu hình máy theo ngày, Quota ngày bác sĩ, Chuyển/Hủy phiên trước 06:30, Danh mục DV CLS, Bảng giá, Quyền & Thiết bị, Ngoại lệ, Duyệt hoàn tiền, Đối soát VNPAY (`querydr`, **không nút đánh dấu đã trả**), AuditLog che CCCD/SĐT (chỉ giữ 3 số cuối), Duyệt miễn/giảm CLS, Cấu hình Chatbot. |
| `legacy-ui/bao-cao.html` | `web_dashboard` | **AD14** (Báo cáo & Xuất CSV) | **Cần tạo mới** | Biểu đồ theo `report_snapshots`, xuất CSV công thức an toàn, chỉ đọc — Admin không có nút thao tác quầy hay lâm sàng. |

---

## 2. Màn hình đã có cần bảo toàn (Không viết lại)

### `frontend/patient-web`
- **P01**: `src/pages/RegisterPage.tsx`
- **P02**: `src/pages/LoginPage.tsx`
- **P03**: `src/pages/ProfilesPage.tsx`
- **P04**: `src/pages/EditProfilePage.tsx`
- **P05**: `src/pages/AddRelativePage.tsx`
- **Extra**: `src/pages/VerificationStatusPage.tsx`

### `frontend/web`
- **S01**: `src/pages/LoginPage.tsx`
- **S02**: `src/layouts/StaffLayout.tsx` + `src/layouts/menus.ts` (giữ hành vi hiện có, mở rộng danh mục điều hướng và S03/S04b)
- **S04(a)**: `src/pages/ForbiddenPage.tsx`
- **AD01**: `src/pages/admin/StaffTab.tsx`
- **AD02**: `src/pages/admin/DoctorsTab.tsx`, `SpecialtiesTab.tsx`, `DoctorRanksTab.tsx`
- **AD03–AD04**: `src/pages/admin/RoomsTab.tsx`
- **AD05**: `src/pages/admin/ShiftsTab.tsx`
- **RC05**: `src/pages/reception/ReceptionPage.tsx` Tab `verifications` (`Xác minh hồ sơ`)

> **Lưu ý về Phòng CLS (`CL01–CL04`)**: Không có file HTML riêng trong `legacy-ui/` và `RoomPage.tsx` hiện mới chỉ là stub 36 dòng; được dựng mới hoàn toàn theo chuẩn Design System Staff và đặc tả `CL01–CL04` (bao gồm upload file draft qua Backend ≤ 20MB, đọc bác sĩ từ ca trực, chặn xem trước khi duyệt sau khi `Published`, xử lý `409` đá phiên `RoomAccount`).
