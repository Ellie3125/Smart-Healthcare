# Contract FR01: Tài khoản và hồ sơ

| Trường | Giá trị |
|---|---|
| Phiên bản | 0.1 |
| Trạng thái | **Draft**; chờ Antigravity phản hồi trước khi chuyển Approved (API-02) |
| Yêu cầu | FR01; NFR01 (kiểm quyền đối tượng), NFR11 (UTC) |
| Module | Identity (bảng `dbo.AppUser`, `dbo.Staff`), Patients (`dbo.Patient`, `dbo.PatientRepresentative`); schema do EF Core migration trong `backend/src/Database` tạo. Đổi cách tạo schema không làm đổi API. |
| Mã nguồn | `backend/src/Modules/Identity/**`, `backend/src/Modules/Patients/**` |

## Quy ước chung

- JSON UTF-8, tên trường camelCase. Enum truyền bằng tên (`"Male"`), không phân biệt hoa thường, không nhận số.
- Thời điểm trả về là ISO-8601 UTC có hậu tố `Z`, chính xác tới mili-giây; client tự hiển thị theo Asia/Ho_Chi_Minh. Ngày sinh là `yyyy-MM-dd`.
- Lỗi theo ProblemDetails (RFC 9457) kèm trường `code` ổn định. Lỗi dữ liệu là `400` với `errors` theo từng trường:

```json
{ "status": 400, "title": "One or more validation errors occurred.", "code": "validation_failed",
  "errors": { "phoneNumber": ["Số điện thoại cần 10 chữ số, bắt đầu bằng 0 hoặc +84."] } }
```

- Xác thực: header `Authorization: Bearer <accessToken>`. Token JWT HS256, hạn 60 phút (cấu hình `Jwt:AccessTokenMinutes`), claim `sub` = UserId, `role` = vai trò. **Chưa có refresh token**: hết hạn thì đăng nhập lại.
- Vai trò: không có hồ sơ nhân viên thì là `Patient`; có thì là `StaffType` (`Doctor`, `Nurse`, `Receptionist`, `Technician`).

## Auth

### POST /api/auth/register
Ẩn danh; giới hạn 10 request/phút/IP (`429` khi vượt).

| Trường | Bắt buộc | Quy tắc |
|---|---|---|
| userName | có | 3–50 ký tự `A-Z a-z 0-9 . _ -` |
| email | có | lưu chữ thường |
| phoneNumber | có | 10 số bắt đầu 0; nhận `+84`, khoảng trắng, dấu chấm và chuẩn hóa về `0xxxxxxxxx` |
| password | có | 8–128 ký tự, có cả chữ và số |

`201` → `{ "userId": "guid" }`. `409 account_already_exists` khi tên đăng nhập, email hoặc số điện thoại đã dùng (không nói rõ trường nào). Email và số điện thoại chưa được xác minh (`emailConfirmed=false`).

### POST /api/auth/login
Ẩn danh; giới hạn như trên. Body `{ "login": "<tên đăng nhập | email | số điện thoại>", "password": "..." }`.

`200` → `{ "accessToken": "...", "tokenType": "Bearer", "expiresAtUtc": "2026-10-29T14:51:36.401Z", "userId": "guid", "role": "Patient" }`.
`401 invalid_credentials` cho mọi trường hợp sai: sai mật khẩu, không có tài khoản, tài khoản hoặc hồ sơ nhân viên bị khóa. Thời gian phản hồi được cân bằng để không dò được tài khoản.

### GET /api/auth/me
Cần token. `200` → `{ id, userName, email, phoneNumber, emailConfirmed, phoneNumberConfirmed, role, createdAtUtc }`.

## Hồ sơ bệnh nhân

Mọi endpoint cần token có `role = Patient` (`401` thiếu token, `403` tài khoản nhân viên). **Quyền đối tượng**: chỉ thấy hồ sơ mà tài khoản có liên kết `PatientRepresentative` đang hoạt động và `Verified`. Hồ sơ không tồn tại và hồ sơ không thuộc quyền trả **cùng** `404 patient_profile_not_found`.

Đối tượng `PatientProfile`:

| Trường | Kiểu | Ghi chú |
|---|---|---|
| id | guid | PatientId nội bộ, khác CCCD |
| fullName | string | 2–150 ký tự, gộp khoảng trắng thừa |
| dateOfBirth | date | từ 1900-01-01 đến hôm nay (giờ VN) |
| gender | `Male` \| `Female` \| `Other` | |
| nationalIdMasked | string \| null | CCCD chỉ hiện 4 số cuối, ví dụ `********0123`; API không bao giờ trả CCCD đầy đủ |
| phoneNumber | string \| null | 10 số bắt đầu 0 |
| address | string \| null | tối đa 300 ký tự |
| relationship | `Self` \| `Child` \| `Parent` \| `Spouse` \| `Other` | quan hệ của tài khoản với người trong hồ sơ |
| version | string | base64 của rowversion; bắt buộc gửi lại khi sửa |
| createdAtUtc | datetime | |

### GET /api/patients
`200` → mảng `PatientProfile`, hồ sơ `Self` đứng đầu rồi theo thứ tự tạo. Không phân trang: số hồ sơ mỗi tài khoản nhỏ.

### POST /api/patients
Body: `relationship` (bắt buộc), `fullName`, `dateOfBirth`, `gender` (bắt buộc), `nationalId` (12 chữ số, tùy chọn), `phoneNumber`, `address` (tùy chọn).
`201` + header `Location` → `PatientProfile`. Tài khoản tạo hồ sơ thì được liên kết `Verified` ngay.

| Mã | Khi nào |
|---|---|
| `409 self_profile_exists` | tài khoản đã có hồ sơ `Self` (mỗi tài khoản tối đa một, kể cả khi gửi song song) |
| `409 national_id_already_registered` | CCCD đã có hồ sơ; liên kết vào hồ sơ có sẵn làm ở quầy tiếp đón, ngoài FR01 |

### GET /api/patients/{patientId}
`200` → `PatientProfile`; `404 patient_profile_not_found`.

### PUT /api/patients/{patientId}
Body như POST nhưng không có `relationship`, thêm `version` (bắt buộc). `nationalId` để `null` là giữ nguyên.
`200` → `PatientProfile` với `version` mới.

| Mã | Khi nào |
|---|---|
| `409 profile_version_conflict` | hồ sơ đã được sửa ở nơi khác sau khi client đọc; tải lại rồi sửa |
| `409 national_id_locked` | đổi CCCD đã ghi nhận; chỉ được bổ sung khi còn trống |
| `409 national_id_already_registered` | CCCD bổ sung trùng hồ sơ khác |
| `404 patient_profile_not_found` | |

### DELETE /api/patients/{patientId}/my-link
Tài khoản tự gỡ liên kết của mình với hồ sơ. `204`. Hồ sơ vẫn được giữ trong hệ thống. `404` như trên.

## Idempotency

Chưa có header `Idempotency-Key` ở bản này. Các lệnh được bảo vệ bằng unique index trong DB:
- Đăng ký trùng tên đăng nhập, email hoặc số điện thoại: `409`.
- Hồ sơ `Self` trùng: `409`.
- CCCD trùng: `409`.

Riêng tạo hồ sơ người thân (`Child`, `Parent`, `Spouse`, `Other`) **khi bấm hai lần có thể tạo hai hồ sơ**. Cần bổ sung idempotency theo DB-04 khi có bảng `ApiIdempotencyRecord`.
