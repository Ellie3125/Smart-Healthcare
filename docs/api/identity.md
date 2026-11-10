# Contract IdentityPatients — HTTP

- Phiên bản: v0.1 Draft (10/11/2026). Claude Code viết theo yêu cầu trưởng nhóm (CLA-FIX-012). Người phụ trách IdentityPatients rà soát; trưởng nhóm duyệt rồi mới chuyển sang Approved.
- Nguồn: code nhánh `fix/identity-v2` (các controller trong `IdentityPatients.Api/Controllers`); ERD v2.4 schema `identity`; DB-05.
- JSON camelCase. Thời gian UTC ISO 8601 (`...Z`). Ngày dạng `yyyy-MM-dd`. Enum truyền dạng chuỗi.
- Lỗi trả `application/problem+json` với `title` (tiếng Việt), `status`, `detail`. **Chưa có extension `code`**: lệch `docs/conventions.md` (W1-03, PR #43, Draft). Xem Q1.

## 1. Xác thực và phân quyền

- **JWT Bearer**, HMAC-SHA256. Issuer/Audience/SigningKey lấy từ `Jwt:*`; khóa ký ở user-secrets hoặc biến môi trường.
- **Claim trong token:**
  - `sub` và `nameidentifier`: AppUserId;
  - `name`: UserName;
  - `role`: một trong `Admin`, `Doctor`, `Nurse`, `Receptionist`, `RoomAccount`, `Patient`;
  - `channel`: `Staff` hoặc `Patient`;
  - `doctor_id`: chỉ có với bác sĩ;
  - `room_id`: chỉ có với tài khoản phòng.
- **Vai trò:** mỗi `AppUser` có đúng một vai trò, lưu ở cột `AppUser.Role`. Không dùng bảng `AspNet*` (DB-05).
- **Phân kênh khi đăng nhập:**
  - kênh nhân viên chỉ nhận vai trò nhân viên;
  - kênh bệnh nhân chỉ nhận `Patient`.

  Vì vậy các policy chỉ cần kiểm vai trò.

| Policy | Vai trò được phép |
|---|---|
| `StaffOnly` | `Admin`, `Doctor`, `Nurse`, `Receptionist`, `RoomAccount` |
| `ReceptionistOnly` | `Receptionist`, `Admin` |
| `AdminOnly` | `Admin` |
| `PatientOnly` | `Patient` |

Chung cho mọi endpoint có policy: 401 khi thiếu token hoặc token không hợp lệ, 403 khi sai vai trò. Hai lỗi này do middleware trả, không có `detail`.

- **Mật khẩu:**
  - hash PBKDF2-SHA256, 600.000 vòng, salt 16 byte, khóa 32 byte;
  - lưu dạng `vòng.salt.hash` (base64);
  - độ dài tối thiểu 6 ký tự.
- **Không khóa tài khoản theo số lần nhập sai.** ERD v2.4 không có cột cho việc này; trưởng nhóm chốt ngày 10/11/2026.

## 2. Auth

### POST `/auth/staff/login` (ẩn danh)

Request:
```json
{ "userName": "letan01", "password": "...", "deviceId": "DEV-01", "deviceIpAddress": "10.0.0.5" }
```
`deviceId` và `deviceIpAddress` không bắt buộc. Hai trường này chỉ dùng khi đăng nhập tài khoản phòng (`RoomAccount`).

200:
```json
{ "accessToken": "<jwt>", "role": "Receptionist", "userName": "letan01", "userId": "<guid>", "doctorId": null, "roomId": null }
```

| Status | Khi nào |
|---|---|
| 401 | Sai tên đăng nhập hoặc mật khẩu, hoặc tài khoản `IsActive = false`. Cùng một thông điệp cho mọi trường hợp; vẫn chạy PBKDF2 khi tên không tồn tại để không lộ qua thời gian phản hồi |
| 403 | Tài khoản `Patient` đăng nhập kênh nhân viên |

**Tài khoản phòng:** khi đăng nhập, mọi phiên `RoomDeviceSession` đang hoạt động của phòng bị đóng, rồi tạo phiên mới. Phiên mới lưu SHA-256 (hex) của token phiên. Unique có lọc `RoomAccountId WHERE IsActive = 1` bảo đảm mỗi phòng tối đa một phiên.

### POST `/auth/patient/login` (ẩn danh)

Request và response như trên, `role` là `Patient`.

| Status | Khi nào |
|---|---|
| 401 | Như kênh nhân viên |
| 403 | Tài khoản nhân viên đăng nhập kênh bệnh nhân |

### GET `/me` (cần token)

200:
```json
{ "userId": "<guid>", "userName": "...", "role": "...", "doctorId": null, "roomId": null }
```

## 3. Hồ sơ bệnh nhân — `api/patient/profiles` (`PatientOnly`)

Một tài khoản quản lý nhiều hồ sơ qua `PatientRepresentative`. Mỗi tài khoản có tối đa một hồ sơ `Self` đang hoạt động (unique có lọc).

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/patient/profiles` | `CreatePatientProfileRequest` | 201 `PatientProfileDto` | 400 dữ liệu sai; 409 trùng CCCD hoặc đã có hồ sơ `Self` |
| GET | `/api/patient/profiles` | — | 200 `PatientProfileDto[]` | — |
| GET | `/api/patient/profiles/{patientId}` | — | 200 `PatientProfileDto` | 403 hồ sơ không thuộc tài khoản; 404 không có |
| PUT | `/api/patient/profiles/{patientId}` | `UpdatePatientProfileRequest` | 200 `PatientProfileDto` | 400; 403; 404; 409 trùng CCCD |
| POST | `/api/patient/profiles/{patientId}/unlink` | — | 204 | 404 |

**`CreatePatientProfileRequest`:**
```json
{ "fullName": "...", "dateOfBirth": "1990-01-01", "gender": "Male", "nationalId": null, "phoneNumber": "...", "address": "...", "relationship": "Self" }
```
- `gender`: `Male`, `Female` hoặc `Other`.
- `relationship`: `Self`, `Child`, `Parent`, `Spouse` hoặc `Other`.
- `nationalId` để trống được (trẻ em). Khi có giá trị thì unique.

**`UpdatePatientProfileRequest`:** giống request tạo, không có `relationship`.

**`PatientProfileDto`:** `patientId`, `representativeId`, `fullName`, `dateOfBirth`, `gender`, `nationalId`, `phoneNumber`, `address`, `relationship`, `verificationStatus` (`Pending`, `Verified`, `Rejected` hoặc `Revoked`), `isActive`.

## 4. Xác minh người đại diện — `api/staff/verifications` (`ReceptionistOnly`)

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/staff/verifications?searchTerm=&status=&page=1&pageSize=20` | — | 200 `PagedResult<VerificationItemDto>` | — |
| POST | `/api/staff/verifications/{representativeId}/verify` | — | 200 `VerificationItemDto` | 404; 409 khi không ở `Pending` (kèm extension `allowedTransitions`) |
| POST | `/api/staff/verifications/{representativeId}/reject` | `{ "reason": "..." }` | 200 | 400 thiếu lý do; 404; 409 |
| POST | `/api/staff/verifications/{representativeId}/revoke` | `{ "reason": "..." }` | 200 | 400 thiếu lý do; 404; 409 khi không ở `Verified` |

- **Chuyển trạng thái hợp lệ:**
  - `Pending` → `Verified`;
  - `Pending` → `Rejected`;
  - `Verified` → `Revoked`.

  `Rejected` và `Revoked` đặt `IsActive = false` và ghi `RevokedAtUtc`.
- **AuditLog:** mỗi lần chuyển trạng thái ghi một dòng `AuditLog` (bảng `identity.PatientRepresentative`) có người thực hiện và lý do.
- **Phân trang:** `pageSize` mặc định 20, tối đa 50.
- **`VerificationItemDto`:** `representativeId`, `patientId`, `appUserId`, `patientFullName`, `dateOfBirth`, `gender`, `maskedNationalId`, `phoneNumber`, `address`, `relationship`, `verificationStatus`, `createdAtUtc`, `revokedAtUtc`.
  - `maskedNationalId` chỉ hiện 4 số cuối, ví dụ `********3456`.
- **`PagedResult<T>`:** `items`, `totalCount`, `page`, `pageSize`.

## 5. Quản trị danh mục và nhân sự — `api/staff/admin` (`AdminOnly`)

Kiểu enum hợp lệ:
- `rankCode`: `SpecialistI`, `SpecialistII`, `Master`, `Doctor`, `AssociateProf` hoặc `Professor`;
- `roomType`: `Clinical`, `Laboratory`, `XRay`, `Ultrasound` hoặc `Endoscopy`;
- `staffType`: `Doctor`, `Nurse` hoặc `Receptionist` (`Technician` trả 400: chưa có vai trò đăng nhập riêng; tài khoản phòng CLS dùng `RoomAccount`);
- `shiftName`: `Morning`, `Afternoon` hoặc `FullDay`.

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/specialties` | — | 200 `SpecialtyAdminDto[]` | — |
| POST | `/specialties` | `{ code, name, description? }` | 201 | 400; 409 trùng mã |
| PUT | `/specialties/{id}` | `{ name, description?, isActive }` | 200 (không đổi được `code`) | 400; 404 |
| GET | `/doctor-ranks` | — | 200 `DoctorRankAdminDto[]` | — |
| POST | `/doctor-ranks` | `{ code, name, displayOrder }` | 201 | 400; 409 |
| PUT | `/doctor-ranks/{id}` | `{ name, displayOrder, isActive }` | 200 | 400; 404 |
| GET | `/rooms?roomType=&isActive=` | — | 200 `RoomAdminDto[]` | — |
| POST | `/rooms` | `{ roomNumber, roomName, roomType, departmentName }` | 201 | 400; 409 trùng số phòng |
| PUT | `/rooms/{id}` | `{ roomName, roomType, departmentName, isActive }` | 200 | 400; 404 |
| GET | `/staff?staffType=&isActive=` | — | 200 `StaffAdminDto[]` | — |
| POST | `/staff` | `{ userName, email, phoneNumber, password, staffCode, fullName, staffType }` | 201 `StaffAdminDto` | 400 (mật khẩu < 6 ký tự, `staffType` sai hoặc `Technician`); 409 trùng tên đăng nhập, email, số điện thoại hoặc mã nhân viên |
| PUT | `/staff/{id}` | `{ fullName, isActive }` | 200 (đồng bộ `AppUser.IsActive`) | 400; 404 |
| POST | `/staff/{id}/toggle-active?isActive=` | — | 200 (đồng bộ `AppUser` và `Doctor`) | 404 |
| GET | `/doctors?specialtyId=&rankId=&isActive=` | — | 200 `DoctorAdminDto[]` | — |
| POST | `/doctors` | `{ staffId, specialtyId, doctorRankId, defaultRoomId?, biography? }` | 201 | 400 nhân sự không phải `Doctor`; 404; 409 đã có bác sĩ |
| PUT | `/doctors/{id}` | `{ specialtyId, doctorRankId, defaultRoomId?, biography?, isActive }` | 200 | 404 |
| POST | `/doctors/{id}/toggle-active?isActive=` | — | 200 | 404 |
| GET | `/shift-assignments?date=&roomId=&staffId=` | — | 200 `ShiftAssignmentDto[]` | — |
| POST | `/shift-assignments` | `{ staffId, roomId, shiftDate, shiftName, startsAtUtc, endsAtUtc, isShiftLeader }` | 201 | 400 sai ca hoặc giờ kết thúc ≤ giờ bắt đầu; 404; 409 nhân viên đã có ca trong ngày, hoặc phòng đã có trưởng ca cho ca đó |
| DELETE | `/shift-assignments/{id}` | — | 204 | 404 |

**DTO trả về:**
- `SpecialtyAdminDto`: `id`, `code`, `name`, `description`, `isActive`.
- `DoctorRankAdminDto`: `id`, `code`, `name`, `displayOrder`, `isActive`.
- `RoomAdminDto`: `id`, `roomNumber`, `roomName`, `roomType`, `departmentName`, `isActive`.
- `StaffAdminDto`: `id`, `appUserId`, `staffCode`, `fullName`, `staffType`, `userName`, `email`, `phoneNumber`, `isActive`, `createdAtUtc`.
- `DoctorAdminDto`: `id`, `staffId`, `fullName`, `staffCode`, `specialtyId`, `specialtyName`, `doctorRankId`, `rankName`, `defaultRoomId`, `biography`, `isActive`.
- `ShiftAssignmentDto`: `id`, `staffId`, `staffName`, `staffCode`, `roomId`, `roomNumber`, `roomName`, `shiftDate`, `shiftName`, `startsAtUtc`, `endsAtUtc`, `isShiftLeader`.

## 6. Tài khoản phòng — `api/staff/admin/rooms` (`AdminOnly`)

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/staff/admin/rooms/{roomId}/account` | `{ userName, email, password, accountCode }` | 201 `RoomAccountDto` | 400; 404 không có phòng; 409 phòng đã có tài khoản, hoặc trùng mã, tên đăng nhập, email |
| GET | `/api/staff/admin/rooms/{roomId}/sessions` | — | 200 `RoomSessionDto[]` (phiên đang hoạt động) | 404 |
| POST | `/api/staff/admin/rooms/accounts/{roomAccountId}/toggle-active?isActive=` | — | 200 `RoomAccountDto`; khi tắt thì đóng mọi phiên | 404 |

**DTO trả về:**
- `RoomAccountDto`: `roomAccountId`, `roomId`, `appUserId`, `accountCode`, `userName`, `email`, `isActive`, `createdAtUtc`.
- `RoomSessionDto`: `sessionId`, `roomAccountId`, `deviceId`, `deviceIpAddress`, `loginAtUtc`, `logoutAtUtc`, `isActive`.

## 7. Câu hỏi mở

- **Q1:** thêm extension `code` (PascalCase) cho mọi lỗi theo `docs/conventions.md` khi W1-03 được duyệt. Người phụ trách IdentityPatients sửa `ApiControllerBase`.
- **Q2:** `AuditLog` của xác minh được ghi sau khi lưu trạng thái, không chung transaction (có `shortcut:` trong `VerificationService`). Cần IdentityPatients thành participant của `IUnitOfWork`.
- **Q3:** hồ sơ không thuộc tài khoản trả 403, nên lộ việc hồ sơ có tồn tại. Đề xuất trả 404 như khi không có hồ sơ.
- **Q4:** cột ngoài ERD v2.4 đang dùng (trưởng nhóm chốt bổ sung vào ERD ngày 10/11/2026):
  - `AppUser.Role`, `DoctorId`, `RoomId`, `RowVersion`;
  - `Doctor.DefaultRoomId`;
  - `Patient.OwnerUserId`.
