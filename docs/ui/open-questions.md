# Open Questions & UI Contract Gaps (`// TODO: nối contract thật`)

> Tài liệu này ghi nhận các endpoint và hành vi Backend chưa có đặc tả chính thức trong `docs/api/*.md` tại thời điểm dựng UI. Tất cả lớp API client liên quan được đánh dấu `// TODO: nối contract thật` và chạy đầy đủ qua **MSW (`VITE_USE_MOCK=true`)** để kiểm thử độc lập, sẵn sàng chuyển sang API thật mà không phải sửa UI component.

---

## 1. Scheduling (`docs/api/scheduling.md` mới duyệt phần W1)

| Hạng mục | API hiện có | Khoảng trống cần chốt với Backend | Ảnh hưởng UI |
| --- | --- | --- | --- |
| Chi tiết slot theo ngày / khung giờ | `GET /api/scheduling/patient/doctors`, `POST .../hold` | Đường dẫn chi tiết một `DoctorDay` (`GET /api/scheduling/patient/doctors/{doctorId}/{serviceDate}`) và payload `BookingAttempt` step confirm/checkout | `P07`, `P08`, `P09`, `P10`, `P11` |
| Đặt lịch hộ người thân (`patientProfileId`) | `HoldRequest` W1 hiện chỉ nhận `{ doctorId, serviceDate }` | Xác nhận trường `patientProfileId` nằm ở bước confirm hay bổ sung vào `hold` | `P08` cho phép chọn hồ sơ của mình hoặc người thân và truyền kèm ở bước xác nhận |
| Lịch của tôi, đổi lịch, hủy lịch | Kế hoạch W2 (`planned`) | Exact routes `/api/scheduling/patient/appointments*`, response `refundPreview` khi hủy (chưa hủy), điều kiện kiểm tra "cùng mức giá" khi đổi lịch | `P12`, `P13`, `P14`, `P15` |
| Walk-in, Check-in, DS cần gọi | Kế hoạch W2 (`planned`) | Exact routes `/api/scheduling/staff/walk-in`, `/checkin`, `/call-list`, trường `CheckInDeadline` & cờ xác nhận giấy tờ | `RC01`, `RC02`, `RC03`, `RC07`, `RC10` |
| Quản lý quota ngày, chuyển/hủy phiên bác sĩ, TV công khai | `DoctorDayEndpoints` mới có mở/đóng 1 ngày | Bulk/list `DoctorDay` theo tuần/ngày, endpoint chuyển/hủy trước `06:30`, endpoint công khai `/api/scheduling/public/display` cho `TV01` | `AD06`, `AD07`, `TV01` |

---

## 2. Billing & Payments (Chưa có `docs/api/billing.md`)

| Hạng mục | Giả định UI tạm thời (MSW) | Cần chốt với Backend | Ảnh hưởng UI |
| --- | --- | --- | --- |
| Tạo phiên VNPAY & Return URL verify | `POST /api/billing/patient/payments/vnpay/create`, `GET /api/billing/patient/payments/vnpay/return-status?vnp_TxnRef=...` | Exact query params truyền về Backend và schema 6 trạng thái (`Verifying`, `Paid`, `Failed`, `Expired`, `AlreadyProcessed`, `Unknown`) | `P09`, `P10`, `P17` |
| Tóm tắt chi phí đợt khám & Hoàn tiền | `GET /api/billing/patient/encounters/{id}/cost-summary`, `/api/billing/patient/refunds*` | Xác nhận 5 chỉ số (`tổng đã trả / tổng đã duyệt hoàn / còn giữ ròng / miễn giảm / hoàn thành công`) và danh sách kênh nhận tiền hợp lệ | `P18`, `P19` |
| Quầy thu ngân, `querydr`, Hoàn tiền mặt, Chốt ca, Duyệt hoàn, Đối soát, Bảng giá, Miễn giảm CLS | `/api/billing/staff/*`, `/api/billing/admin/*` | Exact schema đối soát chốt ca cuối ngày, chứng từ hoàn tiền mặt, payload duyệt hoàn/từ chối và `querydr` VNPAY | `RC04`, `RC06`, `RC08`, `RC09`, `AD09`, `AD12`, `AD13`, `AD16` |

---

## 3. Clinical, Medication, Documents & Workflow

| Hạng mục | Giả định UI tạm thời (MSW) | Cần chốt với Backend | Ảnh hưởng UI |
| --- | --- | --- | --- |
| Hành trình hôm nay (`P16`) & Hồ sơ lịch sử (`P20–P23`) | `/api/clinical/patient/today-journey`, `/encounters*`, `/api/documents/patient/cls/{resultId}/files/{fileId}/download` | Xác nhận `Content-Disposition` / blob stream cho PDF & ảnh CLS để UI không chạm R2 presigned URL trực tiếp; xác nhận endpoint chặn xem hồ sơ khi hồ sơ định danh còn `Pending` | `P16`, `P20`, `P21`, `P22`, `P23` |
| Hàng đợi & Bàn làm việc Điều dưỡng / Bác sĩ / Phòng CLS | `/api/clinical/staff/nurse/*`, `/doctor/*`, `/room/*` | Tên chính xác header/body `version` cho optimistic concurrency (`409 Conflict`), giới hạn file draft CLS (tạm áp dụng `PDF/JPG/PNG <= 20MB`), danh mục ICD-10 và Full-Text catalog thuốc | `NU01–NU02`, `DR01–DR10`, `CL01–CL04` |
| ServiceStation (Quầy) & Cấu hình máy theo ngày | `/api/identity/admin/stations*`, `/device-assignments*` | Xác nhận thuộc về module `identity` hay `scheduling` để gom vào đúng client | `AD03`, `AD10` |

---

## 4. Notifications, Realtime SignalR & Chatbot

| Hạng mục | Giả định UI tạm thời | Cần chốt với Backend | Ảnh hưởng UI |
| --- | --- | --- | --- |
| SignalR Hub URL & Query token | Hub `/hubs/patient` & `/hubs/staff` với `skipNegotiation: true` + `transport: HttpTransportType.WebSockets` | Xác nhận URL hub thực tế, tên event (`JourneyUpdated`, `NotificationReceived`, `QueueUpdated`, `SessionReplaced`) | `P10`, `P16`, `P24`, `S03`, `S04b`, `TV01` |
| Chatbot health endpoint phía trình duyệt | `GET /api/notifications/patient/chat/health` (bọc qua .NET API thay vì gọi trực tiếp FastAPI) | Xác nhận route proxy `/health` qua Backend .NET hay CORS riêng cho Python service | `P25`, `AD17` |

---

## 5. Admin Operations: Exceptions, Reports & AuditLog

| Hạng mục | Giả định UI tạm thời | Cần chốt với Backend | Ảnh hưởng UI |
| --- | --- | --- | --- |
| Exceptions hub (`AD11`), Reports (`AD14`), AuditLogs (`AD15`) | `/api/workflow/admin/exceptions`, `/api/analytics/admin/reports`, `/api/identity/admin/audit-logs` | Xác nhận module prefix thật, trường `beforeJson`/`afterJson` trong AuditLog và định dạng CSV export | `AD11`, `AD14`, `AD15` |
