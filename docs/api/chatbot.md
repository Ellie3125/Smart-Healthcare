# Contract chatbot

- contract_version: **v0.1 Draft** (CB-00, chờ trưởng nhóm duyệt, hạn 21/11/2026; §4 chờ chốt H-06, hạn 20/11/2026)
- Người giữ: theo ownership.md mục 1. Task: CB-00. Bên dùng: CB-01..CB-11 (dịch vụ chatbot), CB-05 (API đề nghị phía .NET), CB-08 (giao diện chat trên web bệnh nhân).
- Nguồn: Phạm vi nghiệp vụ v2.12 §1, §2.1–2.2, §7; Giải pháp kỹ thuật v2.14 §2.1, §8.7, §11.1–11.7; luật BOT-01..BOT-12 (`docs/ai/instructions/chatbot-langchain-ollama.md`).
- Dựa trên: `docs/api/scheduling.md` v0.2 Approved, `docs/api/identity.md` (dev 1026563), `docs/conventions.md` (ProblemDetails + `code`, JSON camelCase, UTC ISO-8601 có `Z`, ngày `yyyy-MM-dd`, `Idempotency-Key`).

## 1. Thành phần và đường đi

```text
web bệnh nhân ──HTTPS──▶ .NET (BE1/BE2), API chat kênh Patient (§3)
                           │ kiểm JWT Patient + quyền trên hồ sơ, gắn ngữ cảnh
                           ▼ mạng Docker nội bộ
                        dịch vụ chatbot FastAPI (§2), không public
                           │ công cụ gọi REST API công khai của .NET bằng token của chính người dùng (§5)
                           │ model: Ollama trên Colab qua ngrok (x-api-key)
web bệnh nhân ──HTTPS──▶ .NET, xác nhận đề nghị (§4.3): bấm nút trên thẻ, không đi qua chatbot
```

- Chatbot chỉ có trên web bệnh nhân, cho vai trò `Patient` kênh `Patient` (BOT-01). Web bệnh nhân không gọi thẳng dịch vụ chatbot.
- Model không có công cụ thay đổi dữ liệu. Đặt, đổi, hủy chỉ thực thi khi người dùng bấm nút xác nhận trên thẻ đề nghị; nút này gọi thẳng .NET (§4.3). Câu "đồng ý" trong ô chat không thực thi gì; chatbot trả lời nhắc bấm nút (BOT-05).
- Model lỗi, timeout hoặc tạm ngưng không đổi DB; người dùng tiếp tục thủ công trên cùng BookingAttempt (BOT-06).

## 2. API nội bộ của dịch vụ chatbot (.NET → FastAPI)

Chỉ mở trong mạng Docker (`http://chatbot:8000`), không qua Nginx/Cloudflare. Mọi request trừ `/health` mang header `X-Chatbot-Key` khớp biến môi trường `CHATBOT_INTERNAL_KEY` (secret, đặt ở cả .NET và chatbot; sai hoặc thiếu trả 401). Đây là lớp chặn thứ hai, lớp thứ nhất là không public.

### 2.1 `POST /v1/turns`

Một lượt chat. .NET gọi sau khi đã kiểm quyền (§3.2).

Header:
- `X-Chatbot-Key`.
- `Authorization: Bearer <access token của người dùng>`: .NET chuyển nguyên token Patient đã xác thực. Chatbot chỉ dùng để gọi API .NET (§5), không giải mã để lấy danh tính, không ghi log, không đưa vào prompt hay checkpoint.

Body:
```json
{
  "conversationId": "<guid>",
  "userId": "<guid>",
  "patientId": "<guid>",
  "messageId": "<guid>",
  "text": "tôi muốn khám tim thứ sáu này",
  "suggestion": null,
  "locale": "vi"
}
```
- `userId`, `patientId`, `conversationId` do .NET điền từ phiên và từ cuộc trò chuyện đã kiểm (§3.2); vào `ChatContext` (Giải pháp §11.3.3), công cụ đọc qua `runtime.context`, không phải tham số model điền (BOT-03).
- `messageId` do .NET cấp mỗi lượt; chatbot ghép thành `Idempotency-Key` khi tạo đề nghị (§5.2).
- Đúng một trong `text` (≤ 1000 ký tự) hoặc `suggestion` (mã nút gợi ý §6.2).

200:
```json
{
  "reply": "Bác sĩ Nguyễn Văn A còn 5 suất ngày 14/11/2026, phí khám 150.000 VND.",
  "intent": "SEARCH_SLOTS",
  "suggestions": ["SEARCH_SLOTS", "VIEW_APPOINTMENTS"],
  "proposal": null,
  "options": [ { "kind": "Doctor", "doctorId": "<guid>", "serviceDate": "2026-11-14", "label": "BS Nguyễn Văn A, còn 5 suất, 150.000 VND" } ]
}
```
- `proposal`: `ProposalDto` (§4.2) khi lượt này tạo đề nghị; web bệnh nhân hiển thị thẻ xác nhận.
- `options`: lựa chọn bấm được (bác sĩ, lịch hẹn, chuyên khoa); bấm gửi lượt mới với `text` mô tả lựa chọn hoặc dùng làm tham số cho đề nghị. Số suất chỉ để tham khảo (Giải pháp §11.6).
- 503 `ChatbotUnavailable`: Colab tắt, lỗi kết nối, 401 từ ngrok hoặc quá `OLLAMA_TIMEOUT_SECONDS`; lượt dừng, không gọi công cụ thay đổi dữ liệu (Giải pháp §11.7.5). Câu đi đường tắt (BOT-11) vẫn trả lời được khi model tạm ngưng.
- 404 `ConversationNotFound`: `conversationId` không thuộc `userId` hoặc đã hết hạn (§3.3).
- 401 `SessionExpired`: API .NET trả 401 với token được chuyển; .NET trả lại cho web bệnh nhân để đăng nhập lại.

### 2.2 `GET /health`

200 `{ "available": true, "model": "qwen2.5:3b" }`. `available=false` khi không gọi được `OLLAMA_BASE_URL/api/tags` trong ~3 giây, bị 401, gặp trang lỗi ngrok hoặc thiếu tag `OLLAMA_MODEL`; kết quả cache vài giây (CB-11, Giải pháp §11.7.5). Không cần `X-Chatbot-Key`, không lộ địa chỉ ngrok.

### 2.3 `DELETE /v1/conversations/{conversationId}`

Xóa checkpoint khi người dùng đóng cuộc trò chuyện hoặc đăng xuất. Header `X-Chatbot-Key`; body `{ "userId": "<guid>" }`, chỉ xóa khi khớp chủ. 204.

## 3. API chat của .NET cho web bệnh nhân

Route theo tiền tố kênh (`docs/conventions.md`, scheduling Q7). Module chứa: **Q-C1** (§9), đề xuất Notifications (`/api/notifications/patient/chat`). Policy `PatientOnly`. Lỗi ProblemDetails có `code`.

### 3.1 Route

| Method + route | Mục đích | Thành công | Lỗi (`code`) |
|---|---|---|---|
| `GET /api/notifications/patient/chat/status` | Chatbot có dùng được không, để ẩn hoặc làm mờ ô chat | 200 `{ "available": bool }` | — |
| `POST /api/notifications/patient/chat/conversations` | Mở cuộc trò chuyện cho một hồ sơ | 201 `{ "conversationId", "patientId", "available" }` | 400 `InvalidInput`; 404 `ProfileNotFound` |
| `POST /api/notifications/patient/chat/conversations/{conversationId}/messages` | Gửi một lượt | 200 như §2.1 (bỏ `intent`) | 400 `InvalidInput`; 404 `ConversationNotFound`; 503 `ChatbotUnavailable`; 429 `TooManyRequests` |
| `DELETE /api/notifications/patient/chat/conversations/{conversationId}` | Đóng cuộc trò chuyện | 204 | 404 `ConversationNotFound` |

- Body mở cuộc trò chuyện: `{ "patientId": "<guid>" }`. Body gửi lượt: `{ "text": "..." }` hoặc `{ "suggestion": "VIEW_APPOINTMENTS" }`.
- `status` gọi `GET /health` của chatbot, cache vài giây. Chatbot không chạy (profile `chatbot` tắt) cũng trả `available=false`.
- Giới hạn tần suất đề xuất: 10 lượt/phút mỗi tài khoản (giá trị chốt khi đo p95 ở CB-09).

### 3.2 Cách .NET truyền ngữ cảnh

| Thông tin | Nguồn | Cách truyền |
|---|---|---|
| userId | claim `sub` của JWT Patient | body `userId` |
| patientId | client chọn hồ sơ khi mở cuộc trò chuyện; .NET kiểm `CanAccessPatientAsync(userId, patientId, AccessLevel.Booking)` ở **mỗi lượt** (quyền có thể bị thu hồi giữa chừng) | body `patientId`; cố định theo cuộc trò chuyện, đổi hồ sơ là mở cuộc mới |
| conversationId | .NET cấp GUID khi mở cuộc trò chuyện | body `conversationId` = `thread_id` của checkpointer |
| token | header `Authorization` của request vào .NET | header `Authorization` sang chatbot; không lưu ở .NET hay chatbot |
| messageId | .NET cấp mỗi lượt | body `messageId` |

Token hết hạn giữa cuộc trò chuyện thì công cụ nhận 401 và lượt trả `SessionExpired`; checkpoint vẫn còn, đăng nhập lại tiếp tục được cùng `conversationId` nếu chưa hết hạn (§3.3).

### 3.3 Gắn cuộc trò chuyện với người dùng

**Q-C2** (§9). Đề xuất: không thêm bảng .NET. Chatbot lưu chủ cuộc trò chuyện (`conversationId`, `userId`, `patientId`, `lastActiveUtc`) cùng tệp SQLite của checkpointer trên volume `/data`; lượt có `userId` hoặc `patientId` không khớp trả 404 `ConversationNotFound`. Hết hạn không hoạt động (đề xuất 30 phút, cùng chính sách phiên) thì xóa cả chủ lẫn checkpoint (Giải pháp §11.4.1). Chủ cuộc trò chuyện là định danh nội bộ của dịch vụ, không vào prompt.

## 4. Đề nghị chờ xác nhận (H-06)

### 4.1 Phương án đề xuất cho H-06: dùng lại `workflow.Operation` + `scheduling.BookingAttempt`, không đổi ERD

| | A. Dùng lại Operation + BookingAttempt (đề xuất) | B. Bảng mới `ChatProposal` |
|---|---|---|
| ERD, migration | Không đổi | Thêm bảng, migration, sửa Giải pháp và ERD trước (kế hoạch, rủi ro H-06) |
| Nội dung đề nghị | Đặt: BookingAttempt `Draft` có `SelectedDoctorId`, `SelectedDoctorRankId`, `SelectedServiceDate`. Đổi: BookingAttempt mới giữ lựa chọn đích, `Operation.TargetEntityId` = lịch cũ. Hủy: chỉ `Operation.TargetEntityId` = lịch hẹn | Cột riêng |
| Trạng thái | `Operation.Status`: `Pending` = chờ xác nhận; `Succeeded`; `NeedsAction` (hết chỗ, quá mốc...); `Failed` (từ chối, hết hạn) | Cột riêng |
| Thời hạn | `Operation.CreatedAtUtc` + `ProposalTtl` (cấu hình, đề xuất 10 phút), so bằng giờ DB | Cột `ExpiresAtUtc` |
| Chủ | `Operation.InitiatedByUserId`, PatientId lấy qua BookingAttempt hoặc Appointment | Cột riêng |
| Nhược | `Operation` mang thêm nghĩa "chờ xác nhận" ở trạng thái `Pending`; thời hạn là cấu hình, không lưu theo dòng | Tốn công, đụng ERD cuối tuần 3 |

Ánh xạ:

| Hành động | `OperationType` | `TargetEntityType`, `TargetEntityId` | `BookingAttemptId` |
|---|---|---|---|
| Book | `HoldSlot` | `BookingAttempt`, attempt mới | attempt mới (`Draft`, lựa chọn đã điền) |
| Reschedule | `RescheduleBooking` | `Appointment`, lịch cần đổi | attempt mới giữ bác sĩ + ngày đích |
| Cancel | `CancelBooking` | `Appointment`, lịch cần hủy | null |

`proposalId` = `Operation.Id`. Tóm tắt (`summary`) do .NET dựng từ dữ liệu lúc đọc, không lưu chuỗi do model sinh.

### 4.2 `ProposalDto`

```json
{
  "proposalId": "<guid>",
  "action": "Book",
  "status": "Pending",
  "patientId": "<guid>",
  "summary": "Đặt lịch khám BS Nguyễn Văn A ngày 14/11/2026, phí khám 150.000 VND",
  "appointmentId": null,
  "doctorId": "<guid>",
  "serviceDate": "2026-11-14",
  "fee": 150000,
  "refundAmount": null,
  "currency": "VND",
  "expiresAtUtc": "2026-11-10T08:10:00Z",
  "result": null
}
```
- `action`: `Book`, `Reschedule`, `Cancel`. `status`: `Pending`, `Confirmed`, `Rejected`, `Expired`, `NeedsAction`.
- Hủy: `refundAmount` theo cùng logic `cancellation-preview` (trước 05:00 hoàn đủ, từ 05:00 không hoàn), tính lúc tạo; lúc xác nhận Backend tính lại theo giờ DB và trả số thật trong `result`.
- `result` khi đã xác nhận: Book → `{ "bookingAttemptId", "expiresAtUtc", "next": "Payment" }` (giữ chỗ 5 phút, web bệnh nhân chuyển sang bước thanh toán VNPAY như đường thủ công); Reschedule → `{ "appointmentId", "appointmentNumber" }`; Cancel → `{ "refund": "Full" | "None" }`.

### 4.3 Route (CB-05, Scheduling, `PatientOnly`)

| Method + route | Ai gọi | Thành công | Lỗi (`code`) |
|---|---|---|---|
| `POST /api/scheduling/patient/proposals` | công cụ chatbot | 201 `ProposalDto` | 400 `InvalidInput`; 404 `AppointmentNotFound`, `DoctorNotFound`, `DayNotFound`; 409 `InvalidAppointmentState`, `DayClosed`, `CutoffPassed`, `Full`, `DifferentFee`, `SameDay`, `IdempotencyConflict` |
| `GET /api/scheduling/patient/proposals/{proposalId}` | web bệnh nhân, chatbot | 200 `ProposalDto` (`Expired` tính lúc đọc) | 404 `ProposalNotFound` |
| `POST /api/scheduling/patient/proposals/{proposalId}/confirm` | **chỉ web bệnh nhân** (nút trên thẻ) | 200 `ProposalDto` `Confirmed` | 404 `ProposalNotFound`; 409 `ProposalExpired`, `ProposalAlreadyHandled`, `Full`, `DayClosed`, `CutoffPassed`, `RescheduleCutoffPassed`, `DifferentFee`, `VersionConflict`, `InvalidAppointmentState`, `AlreadyClaimed`, `IdempotencyConflict`, `RequestInProgress` |
| `POST /api/scheduling/patient/proposals/{proposalId}/reject` | web bệnh nhân | 200 `ProposalDto` `Rejected` | 404; 409 `ProposalAlreadyHandled` |

- Body tạo: `{ "action": "Book", "patientId", "doctorId", "serviceDate" }`; `{ "action": "Reschedule", "patientId", "appointmentId", "doctorId", "serviceDate" }`; `{ "action": "Cancel", "patientId", "appointmentId" }`. Header `Idempotency-Key` bắt buộc.
- Tạo đề nghị chỉ đọc và kiểm sơ bộ (quyền, trạng thái lịch, ngày mở, cùng mức phí khi đổi), không giữ suất, không giữ transaction.
- Xác nhận: header `Idempotency-Key` bắt buộc. Backend đọc lại trên primary, kiểm chủ, quyền `Booking` trên hồ sơ, `Pending`, chưa quá hạn theo giờ DB, rồi gọi đúng use case của đường thủ công: Book → `HoldService` (kênh `BookingChannel.Chatbot`); Reschedule, Cancel → `AppointmentChangeService`. Hết suất lúc xác nhận trả 409 `Full`, đề nghị chuyển `NeedsAction`, không ghi gì khác (CB-05, Giải pháp §11.5). Gửi lặp cùng khóa trả kết quả cũ.
- `confirm` không phải công cụ của chatbot và không nằm trong whitelist (BOT-04, BOT-05). Chatbot chỉ đọc lại `GET .../proposals/{id}` để trình bày kết quả.
- Chatbot không ghi đề nghị vào checkpoint ngoài `proposal_id` (Giải pháp §11.3.2).

## 5. Intent ↔ công cụ ↔ endpoint .NET

Công cụ gọi REST API công khai của .NET với header `Authorization` của người dùng (§2.1), giống web bệnh nhân (BOT-04). `patientId` trong URL/body lấy từ `runtime.context`, không do model điền. Mọi API kiểm lại quyền; công cụ không gọi contract module hay DB.

### 5.1 Bảng chính

| Intent | Công cụ | Endpoint .NET | Trạng thái endpoint |
|---|---|---|---|
| SEARCH_SLOTS | `search_doctors(service_date, specialty_code?, doctor_rank_code?)` | `GET /api/scheduling/patient/doctors?serviceDate&specialtyId&doctorRankId&page&pageSize` | **Có** (scheduling §4.3 Approved). Thiếu tên chuyên khoa/cấp bậc cho bệnh nhân: E5 |
| BOOK | `prepare_booking(doctor_id, service_date)` | `POST /api/scheduling/patient/proposals` action `Book` | **Thiếu**: E1 (CB-05) |
| VIEW_APPOINTMENTS | `list_my_appointments(status?)` | `GET /api/scheduling/patient/appointments?patientId` | **Thiếu**: E2 (scheduling §5 dự kiến) |
| RESCHEDULE | `list_my_appointments`, `search_doctors`, `prepare_reschedule(appointment_id, doctor_id, service_date)` | `POST .../proposals` action `Reschedule` | **Thiếu**: E1, E2 |
| CANCEL | `list_my_appointments`, `prepare_cancel(appointment_id)` | `POST .../proposals` action `Cancel` | **Thiếu**: E1, E2 |
| VIEW_TICKET | `get_appointment(appointment_id)` | `GET /api/scheduling/patient/appointments/{appointmentId}`: mã lịch hẹn, số thứ tự sau check in | **Thiếu**: E2; số thứ tự phụ thuộc W3-02 và Q1 scheduling |
| VIEW_EXAM_PAYMENT | `get_exam_payment(appointment_id \| booking_attempt_id)` | `GET /api/billing/patient/exam-payments?bookingAttemptId` | **Thiếu**: E3 (Billing, ngoài vùng) |
| SUGGEST_SPECIALTY | không gọi API nghiệp vụ; tra bảng YAML (CB-06), rồi `list_specialties()` khi không khớp | `GET /api/patient/catalog/specialties` | **Thiếu**: E5 (Identity, ngoài vùng) |
| OUT_OF_SCOPE | không có công cụ | — | — |
| (đọc kết quả đề nghị) | `get_proposal(proposal_id)` | `GET /api/scheduling/patient/proposals/{proposalId}` | **Thiếu**: E1 |
| (tiếp tục đặt dở) | `get_booking_progress(booking_attempt_id)` | `GET /api/scheduling/patient/booking-attempts/{id}` | **Có** (scheduling §4.4 Approved) |

Công cụ không có: xác nhận đề nghị, giữ chỗ trực tiếp, tạo thanh toán, hủy/đổi trực tiếp, mọi công cụ HTTP/SQL tùy ý.

### 5.2 Quy ước công cụ

- Tên công cụ, tham số và DTO Pydantic theo bảng trên; DTO là Data Schema (Giải pháp §11.3.1): `SlotOption`, `AppointmentOut`, `TicketOut` (tách `appointment_number` và `queue_number`), `ExamPaymentOut`, `ProposalOut`. Chatbot đổi `specialty_code`/`doctor_rank_code` ra Id bằng danh mục E5, cache có hạn (Giải pháp §11.6).
- `prepare_*` gửi `Idempotency-Key` = `chat:{conversationId}:{messageId}:{tool}`; gọi lặp trong cùng lượt trả cùng đề nghị.
- Timeout mỗi lời gọi API 5 giây; lỗi 5xx hoặc timeout thì trả lời "chưa lấy được dữ liệu, thử lại hoặc thao tác thủ công", không đoán (BOT-06).
- Ánh xạ lỗi .NET → câu trả lời theo `code` (không theo `title`): `Full` → hết suất, gợi ý ngày/bác sĩ khác; `CutoffPassed`/`RescheduleCutoffPassed` → đã qua mốc; `DifferentFee` → đổi chỉ sang bác sĩ cùng mức phí; 404 → "không tìm thấy" (không phân biệt không có và không có quyền); 401 → `SessionExpired`.
- Nhóm intent cho đường gọi model (BOT-11): tra cứu (`search_doctors`, `list_my_appointments`, `get_appointment`, `get_exam_payment`, `get_booking_progress`), thay đổi (`prepare_*`, `get_proposal`); chỉ gửi nhóm liên quan.

## 6. Nút gợi ý và đường tắt

### 6.1 Luồng

Bộ định tuyến Python chạy trước model (BOT-11, Giải pháp §11.3.5). Nút gợi ý hoặc câu khớp mẫu trong bảng cấu hình → intent + slot → công cụ → câu mẫu, không gọi model. Câu tự do → model, chỉ với nhóm công cụ liên quan. Kiểm `red_flags` của gợi ý chuyên khoa chạy trước mọi đường (BOT-12).

### 6.2 Mã nút gợi ý (`suggestion`)

`SEARCH_SLOTS` ("Tìm lịch khám"), `VIEW_APPOINTMENTS` ("Xem lịch của tôi"), `RESCHEDULE` ("Đổi lịch"), `CANCEL` ("Hủy lịch"), `VIEW_TICKET` ("Xem vé"), `VIEW_EXAM_PAYMENT` ("Thanh toán khám"), `SUGGEST_SPECIALTY` ("Tôi nên khám khoa nào"). Nhãn hiển thị do web bệnh nhân giữ; mã trùng tên intent.

## 7. Dữ liệu không được gửi tới model, không ghi log

Access token, `userId`, `patientId`, `conversationId`, `proposalId` dạng thô, CCCD, số điện thoại, dữ liệu thanh toán (BOT-10). Công cụ trả cho model bản rút gọn: tên bác sĩ, ngày, số suất tham khảo, phí, trạng thái, mã lịch hẹn; Id dùng để gọi tiếp được giữ trong state theo chỉ số lựa chọn (`option 1`, `option 2`). Log chỉ ghi intent, tên công cụ, mã HTTP, thời gian; không ghi nội dung tin nhắn hay triệu chứng.

## 8. Endpoint còn thiếu

| # | Endpoint | Module, người làm | Task | Ghi chú |
|---|---|---|---|---|
| E1 | `POST/GET /api/scheduling/patient/proposals`, `.../confirm`, `.../reject` (§4.3) | Scheduling, vùng chung | CB-05 (CX) | Chờ H-06 chốt §4.1 |
| E2 | `GET /api/scheduling/patient/appointments`, `GET .../appointments/{id}` | Scheduling, vùng chung | W2-07 HTTP (scheduling §5) | DTO cần `appointmentNumber`, `queueNumber?`, `status`, `allowedActions` (Giải pháp §11.3.1) |
| E3 | `GET /api/billing/patient/exam-payments?bookingAttemptId=` (hoặc theo `appointmentId`): trạng thái `Pending`, `Paid`, `Failed`, `Unknown`, `RefundPending`, `Refunded` | Billing, ngoài vùng | đề xuất cho TV phụ trách VNPAY | Cũng cần cho web bệnh nhân thủ công |
| E4 | API chat .NET (§3) + chuyển `available` | Q-C1; nếu Notifications là vùng chung | CB-11 (phần .NET) | Gọi FastAPI qua `ChatbotOptions` (`BaseUrl`, `InternalKey` từ biến môi trường) |
| E5 | `GET /api/patient/catalog/specialties`, `GET /api/patient/catalog/doctor-ranks` (Id, Code, Name, đang hoạt động), `PatientOnly` | IdentityPatients, ngoài vùng | đề xuất cho người phụ trách Identity | Hiện chỉ có bản `AdminOnly`; tìm bác sĩ trả Id, chatbot và web bệnh nhân cần tên |

## 9. Câu hỏi mở

| # | Câu hỏi | Đề xuất | Trạng thái |
|---|---|---|---|
| H-06 | Lưu đề nghị chờ xác nhận ở đâu? | Phương án A §4.1: `Operation` (`Pending`) + `BookingAttempt`, không đổi ERD; thời hạn = `CreatedAtUtc` + `ProposalTtl` 10 phút | Chờ trưởng nhóm, hạn 20/11 |
| Q-C1 | API chat .NET đặt ở module nào? | Notifications (`/api/notifications/patient/chat`): module chung, không phải nghiệp vụ lịch; chỉ chuyển tiếp, không có bảng | Chờ trưởng nhóm |
| Q-C2 | Gắn `conversationId` với người dùng ở đâu? | Trong SQLite của chatbot cùng checkpointer (§3.3), không thêm bảng .NET | Chờ trưởng nhóm |
| Q-C3 | Thời hạn cuộc trò chuyện và đề nghị | Cuộc trò chuyện 30 phút không hoạt động; đề nghị 10 phút | Chờ trưởng nhóm; số chốt sau đo CB-09 |
| Q-C4 | Xác thực .NET → chatbot | Không public + `X-Chatbot-Key` (`CHATBOT_INTERNAL_KEY`) | Chờ trưởng nhóm |
| Q-C5 | Model có được biết tên bệnh nhân không? | Không cần; câu trả lời xưng "bạn", tên hồ sơ do web bệnh nhân hiển thị | Đề xuất |

## 10. Lịch sử

| Version | Ngày | Thay đổi |
|---|---|---|
| v0.1 Draft | 10/11/2026 | Bản đầu (CB-00): API nội bộ chatbot, API chat .NET, ngữ cảnh, đề nghị chờ xác nhận (đề xuất H-06), bảng intent ↔ công cụ ↔ endpoint, endpoint còn thiếu E1–E5 |
