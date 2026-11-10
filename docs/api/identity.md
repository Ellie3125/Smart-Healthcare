# Contract IdentityPatients — HTTP

- Phiên bản: v0.2 Draft (10/11/2026). Claude Code viết theo yêu cầu trưởng nhóm (CLA-FIX-012, sửa ở CLA-FIX-013). Người phụ trách IdentityPatients rà soát; trưởng nhóm duyệt rồi mới chuyển sang Approved.
- Nguồn: Phạm vi nghiệp vụ v2.12 (mục 2.1 ý 7, 2.3); Giải pháp kỹ thuật v2.14 (§8.6, "Kênh và vai trò"); ERD v2.4 schema `identity`; DB-05; code trong `IdentityPatients.Api/Controllers`.
- JSON camelCase. Thời gian UTC ISO 8601 (`...Z`). Ngày dạng `yyyy-MM-dd`. Enum truyền dạng chuỗi.
- Lỗi do module trả là `application/problem+json`, gồm `title` (tiếng Việt), `status`, `detail` và extension **`code`** (PascalCase, `docs/conventions.md` §4). Frontend và chatbot rẽ nhánh theo `code`. Khi không có mã riêng thì `code` theo status: 400 `InvalidInput`, 403 `Forbidden`, 404 `NotFound`, 409 `Conflict`. 401/403 do middleware xác thực trả thì không có thân.
- Tiền tố route `api/staff/...`, `api/patient/...` khác quy ước `api/<module>/<kênh>/...` của `docs/conventions.md` (PR #43, Draft). Chờ trưởng nhóm chốt một kiểu (PROGRESS mục 5).

## 1. Xác thực và phân quyền

- **JWT Bearer**, HMAC-SHA256. Issuer, Audience và SigningKey lấy từ `Jwt:*`. Khóa ký chỉ đặt ở user-secrets hoặc biến môi trường `Jwt__SigningKey`; thiếu hoặc ngắn hơn 32 byte thì ứng dụng không khởi động.
- **Claim trong token:**
  - `sub` và `nameidentifier`: AppUserId;
  - `name`: UserName;
  - `role`: một trong `Admin`, `Doctor`, `Nurse`, `Receptionist`, `RoomAccount`, `Patient`;
  - `channel`: `Staff` hoặc `Patient`;
  - `doctor_id`: chỉ có với bác sĩ đã có hồ sơ Doctor;
  - `room_id`: chỉ có với tài khoản phòng.
- **Vai trò suy ra lúc đăng nhập, không lưu cột** (ERD v2.4, không dùng bảng `AspNet*`):
  - có `RoomAccount` → `RoomAccount`;
  - có `Staff` → theo `StaffType`: `Doctor`, `Nurse`, `Receptionist`, `Admin`;
  - không có cả hai → `Patient`.

  `Technician` chưa có vai trò đăng nhập. `Staff` hoặc `RoomAccount` bị vô hiệu hóa thì không đăng nhập được. Mỗi tài khoản đúng một vai trò.
- **Policy:** mỗi policy kiểm cả vai trò và claim `channel`. Token đúng vai trò nhưng sai kênh bị 403. Admin chỉ có trong `AdminOnly`.

| Policy | Vai trò | Kênh | Khu vực web nhân viên |
|---|---|---|---|
| `AdminOnly` | `Admin` | `Staff` | `/admin` |
| `ReceptionistOnly` | `Receptionist` | `Staff` | `/reception` |
| `NurseOnly` | `Nurse` | `Staff` | `/nurse` (chưa có endpoint) |
| `DoctorOnly` | `Doctor` | `Staff` | `/doctor` (chưa có endpoint) |
| `RoomAccountOnly` | `RoomAccount` | `Staff` | `/room` (chưa có endpoint) |
| `PatientOnly` | `Patient` | `Patient` | web bệnh nhân |
| `AnyChannel` | mọi vai trò | kênh phải khớp loại vai trò | `/me` |

- **Mọi endpoint có policy**, trừ danh sách công khai:
  - `POST /auth/staff/login`, `POST /auth/patient/login`;
  - `GET /api/billing/payments/vnpay/ipn` (callback VNPAY, xác minh bằng chữ ký);
  - `/`, `/openapi/v1.json`, `/swagger` (chỉ Development).

  Test `EndpointAuthorizationInventoryTests` (ArchitectureTests) duyệt mọi endpoint đã map và đỏ khi có endpoint thiếu policy.
- **Mật khẩu:**
  - băm PBKDF2-HMAC-SHA256, salt ngẫu nhiên 16 byte, 600.000 vòng, khóa 32 byte, so sánh thời gian không đổi;
  - lưu dạng `vòng.salt.hash` (base64), 76 ký tự, vừa cột `PasswordHash` nvarchar(128);
  - độ dài tối thiểu 6 ký tự.
- **Không khóa tài khoản theo số lần nhập sai** (ERD v2.4 không có cột cho việc này).

### Ma trận endpoint → policy → vai trò

| Endpoint | Policy | Được phép | Mọi vai trò khác |
|---|---|---|---|
| `POST /auth/staff/login`, `POST /auth/patient/login` | công khai | — | — |
| `GET /me` | `AnyChannel` | mọi vai trò, đúng kênh | 403 khi sai kênh |
| `/api/patient/profiles/**` | `PatientOnly` | Patient (kênh Patient) | 403 |
| `/api/staff/verifications/**` | `ReceptionistOnly` | Receptionist (kênh Staff) | 403, kể cả Admin |
| `/api/staff/admin/**`, `/api/staff/admin/rooms/**` | `AdminOnly` | Admin (kênh Staff) | 403 |
| `/api/scheduling/admin/doctor-days/**` | `AdminOnly` | Admin (kênh Staff) | 403 |

## 2. Auth

### POST `/auth/staff/login` (công khai)

Request:
```json
{ "userName": "letan01", "password": "...", "deviceId": "DEV-01", "deviceIpAddress": "10.0.0.5" }
```
`deviceId` và `deviceIpAddress` không bắt buộc. Hai trường này chỉ dùng khi đăng nhập tài khoản phòng (`RoomAccount`).

200:
```json
{ "accessToken": "<jwt>", "role": "Receptionist", "userName": "letan01", "userId": "<guid>", "doctorId": null, "roomId": null }
```

| Status | `code` | Khi nào |
|---|---|---|
| 401 | `InvalidCredentials` | Sai mật khẩu, tài khoản không tồn tại, bị vô hiệu hóa, Technician, hoặc tài khoản `Patient` đăng nhập kênh này. Mọi trường hợp cùng `title` và `detail`; vẫn chạy PBKDF2 khi tên không tồn tại để không lộ thông tin qua thời gian phản hồi |

**Tài khoản phòng:** đăng nhập chạy trong một unit of work có khóa hàng `RoomAccount`. Mọi phiên `RoomDeviceSession` đang hoạt động bị đóng rồi tạo phiên mới (lưu SHA-256 hex của token phiên); khi có phiên cũ bị đóng thì ghi `AuditLog`. Quy tắc mỗi phòng tối đa một phiên hoạt động do Backend kiểm (ERD v2.4), không có unique trong DB.

### POST `/auth/patient/login` (công khai)

Request và response như trên, `role` là `Patient`.

| Status | `code` | Khi nào |
|---|---|---|
| 401 | `InvalidCredentials` | Như kênh nhân viên, kể cả tài khoản nhân viên hay tài khoản phòng đăng nhập kênh này |

### GET `/me` (`AnyChannel`)

200:
```json
{ "userId": "<guid>", "userName": "...", "role": "...", "doctorId": null, "roomId": null }
```

## 3. Hồ sơ bệnh nhân — `api/patient/profiles` (`PatientOnly`)

Một tài khoản quản lý nhiều hồ sơ qua `PatientRepresentative`. Mỗi tài khoản có tối đa một hồ sơ `Self` đang hoạt động. Backend kiểm trong unit of work có khóa hàng `AppUser`; DB không có unique cho quy tắc này.

| Method | Route | Body | Thành công | Lỗi (`code`) |
|---|---|---|---|---|
| POST | `/api/patient/profiles` | `CreatePatientProfileRequest` | 201 `PatientProfileDto` | 400 `InvalidInput`; 409 `SelfProfileExists`; 409 `Conflict` trùng CCCD |
| GET | `/api/patient/profiles` | — | 200 `PatientProfileDto[]` | — |
| GET | `/api/patient/profiles/{patientId}` | — | 200 `PatientProfileDto` | 404 `ProfileNotFound` khi không có hoặc không thuộc tài khoản |
| PUT | `/api/patient/profiles/{patientId}` | `UpdatePatientProfileRequest` | 200 `PatientProfileDto` | 400 `InvalidInput`; 404 `ProfileNotFound`; 409 `Conflict` trùng CCCD |
| POST | `/api/patient/profiles/{patientId}/unlink` | — | 204 | 404 `NotFound` |

Hồ sơ không thuộc tài khoản trả cùng 404 và cùng nội dung như hồ sơ không có, để không lộ việc hồ sơ tồn tại.

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
- **AuditLog:** mỗi lần chuyển trạng thái ghi một dòng `AuditLog` (bảng `identity.PatientRepresentative`) có người thực hiện và lý do, trong cùng unit of work với thay đổi trạng thái. Hàng `PatientRepresentative` được khóa, nên hai tiếp tân thao tác cùng hồ sơ thì người sau thấy trạng thái mới.
- **Phân trang:** `pageSize` mặc định 20, tối đa 50.
- **`VerificationItemDto`:** `representativeId`, `patientId`, `appUserId`, `patientFullName`, `dateOfBirth`, `gender`, `maskedNationalId`, `phoneNumber`, `address`, `relationship`, `verificationStatus`, `createdAtUtc`, `revokedAtUtc`.
  - `maskedNationalId` chỉ hiện 4 số cuối, ví dụ `********3456`.
- **`PagedResult<T>`:** `items`, `totalCount`, `page`, `pageSize`.

## 5. Quản trị danh mục và nhân sự — `api/staff/admin` (`AdminOnly`)

Kiểu enum hợp lệ:
- `rankCode`: `SpecialistI`, `SpecialistII`, `Master`, `Doctor`, `AssociateProf` hoặc `Professor`;
- `roomType`: `Clinical`, `Laboratory`, `XRay`, `Ultrasound` hoặc `Endoscopy`;
- `staffType` khi tạo nhân sự: `Doctor`, `Nurse`, `Receptionist` hoặc `Admin`. `Technician` trả 400 vì chưa có vai trò đăng nhập; tài khoản phòng CLS dùng `RoomAccount`;
- `shiftName`: `Morning`, `Afternoon` hoặc `FullDay`.

**Quy tắc nhân sự loại Admin:**
- Chỉ Admin tạo, sửa hoặc vô hiệu hóa nhân sự loại Admin; người gọi không phải Admin nhận 403 `AdminManagedByAdmin`.
- Không vô hiệu hóa được Admin đang hoạt động cuối cùng: 409 `LastActiveAdmin`. Backend khóa các Admin đang hoạt động khi kiểm, nên hai lệnh đồng thời không cùng vượt qua.
- Chưa có API đổi loại nhân sự. Khi thêm thì áp cùng hai quy tắc trên.

| Method | Route | Body | Thành công | Lỗi (`code`) |
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
| POST | `/staff` | `{ userName, email, phoneNumber, password, staffCode, fullName, staffType }` | 201 `StaffAdminDto` | 400 `InvalidInput`; 403 `AdminManagedByAdmin`; 409 `UserNameTaken`, `ContactTaken`, `StaffCodeTaken` |
| PUT | `/staff/{id}` | `{ fullName, isActive }` | 200 (đồng bộ `AppUser.IsActive`) | 400; 403 `AdminManagedByAdmin`; 404; 409 `LastActiveAdmin` |
| POST | `/staff/{id}/toggle-active?isActive=` | — | 200 (đồng bộ `AppUser` và `Doctor`) | 403 `AdminManagedByAdmin`; 404; 409 `LastActiveAdmin` |
| GET | `/doctors?specialtyId=&rankId=&isActive=` | — | 200 `DoctorAdminDto[]` | — |
| POST | `/doctors` | `{ staffId, specialtyId, doctorRankId, biography? }` | 201 | 400 nhân sự không phải `Doctor`; 404; 409 đã có bác sĩ |
| PUT | `/doctors/{id}` | `{ specialtyId, doctorRankId, biography?, isActive }` | 200 | 404 |
| POST | `/doctors/{id}/toggle-active?isActive=` | — | 200 | 404 |
| GET | `/shift-assignments?date=&roomId=&staffId=` | — | 200 `ShiftAssignmentDto[]` | — |
| POST | `/shift-assignments` | `{ staffId, roomId, shiftDate, shiftName, startsAtUtc, endsAtUtc, isShiftLeader }` | 201 | 400 sai ca hoặc giờ kết thúc ≤ giờ bắt đầu; 404; 409 `ShiftAlreadyAssigned` (nhân viên đã có ca trong ngày); 409 `ShiftLeaderExists` (phòng đã có trưởng ca cho ca đó) |
| DELETE | `/shift-assignments/{id}` | — | 204 | 404 |

Phòng làm việc của bác sĩ theo ngày lấy từ `StaffShiftAssignment.RoomId`; không có phòng mặc định của bác sĩ. Hai quy tắc phân ca do Backend kiểm trong unit of work có khóa hàng `Staff` và `Room` (ERD v2.4 không có unique).

**DTO trả về:**
- `SpecialtyAdminDto`: `id`, `code`, `name`, `description`, `isActive`.
- `DoctorRankAdminDto`: `id`, `code`, `name`, `displayOrder`, `isActive`.
- `RoomAdminDto`: `id`, `roomNumber`, `roomName`, `roomType`, `departmentName`, `isActive`.
- `StaffAdminDto`: `id`, `appUserId`, `staffCode`, `fullName`, `staffType`, `userName`, `email`, `phoneNumber`, `isActive`, `createdAtUtc`.
- `DoctorAdminDto`: `id`, `staffId`, `fullName`, `staffCode`, `specialtyId`, `specialtyName`, `doctorRankId`, `rankName`, `biography`, `isActive`.
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

## 7. Câu hỏi đã xử lý ở CLA-FIX-013 (10/11/2026)

- **Q1 (đã xử lý):** mọi lỗi do module trả có extension `code`.
- **Q2 (đã xử lý):** `AuditLog` của xác minh ghi trong cùng unit of work với thay đổi trạng thái; `IdentityPatientsDbContext` là participant của `IUnitOfWork` ở SmartHealthcare.Api. Test `Verify_InsideFailedUnitOfWork_RollsBackStatusAndAuditLogTogether` kiểm cả hai cùng rollback.
- **Q3 (đã xử lý):** hồ sơ không thuộc tài khoản trả 404 `ProfileNotFound`.
- **Q4 (đã xử lý):** schema `identity` theo đúng ERD v2.4 gốc; các cột ngoài ERD đã bỏ bằng migration `AlignIdentityWithErd`.

## 8. Còn mở

- Tiền tố route (`api/staff/...` so với `api/identity-patients/staff/...` của `docs/conventions.md`): chờ trưởng nhóm chốt.
- Endpoint của bác sĩ, điều dưỡng và tài khoản phòng chưa có; khi thêm thì dùng `DoctorOnly`, `NurseOnly` hoặc `RoomAccountOnly`.
