# FR01-account-profile: Tài khoản và hồ sơ (Backend)

| Trường | Giá trị |
|---|---|
| task_id | FR01-account-profile |
| owner_agent | Claude Code, viết vào phạm vi của Codex theo ủy quyền trực tiếp của chủ repo ngày 29/10/2026; cần trưởng nhóm review |
| owner_member | Duongphan-05 |
| base_commit | 3d7030c |
| branch | feature/fr01-account-profile |
| requirement_ids | FR01, NFR01, NFR11; MED-03, SEC-01, SEC-02, DB-03, DB-05, DB-06 |
| allowed_paths | backend/**, tests/backend/**, tests/integration/**, docs/api/fr01-account-profile.md, docs/tasks/codex/FR01-*, docs/sessions/codex/FR01-*, docs/evidence/codex/FR01-* |
| contract_version | docs/api/fr01-account-profile.md v0.1 Draft |
| status | In review |
| depends_on | Migration `backend/src/Database` (46 bảng theo ERD v2.2); FR01 dùng AppUser, Staff, Patient, PatientRepresentative |

## Phạm vi
Đăng ký, đăng nhập (JWT), xem tài khoản của mình; tạo, xem, sửa hồ sơ bệnh nhân của mình và người mình đại diện; gỡ liên kết. Backend kiểm quyền trên từng hồ sơ, không tin PatientId do client gửi. Không gồm giao diện (thuộc Antigravity), xác minh email/OTP, refresh token, liên kết vào hồ sơ có sẵn (luồng quầy).

## Acceptance criteria
1. Đăng ký bắt buộc email và số điện thoại, cả hai duy nhất; trùng trả 409, kể cả khi gửi song song.
2. Đăng nhập bằng tên đăng nhập, email hoặc số điện thoại; mọi lỗi xác thực trả cùng một 401.
3. Tài khoản A không đọc, sửa, gỡ được hồ sơ của B; trả 404 giống hồ sơ không tồn tại.
4. Mỗi tài khoản tối đa một hồ sơ Self, kể cả khi gửi song song.
5. Sửa hồ sơ bằng phiên bản cũ trả 409, không ghi đè.
6. CCCD không bao giờ trả đầy đủ; CCCD trùng trả 409; CCCD đã có không tự sửa được.
7. Tài khoản nhân viên không dùng được API tự phục vụ của bệnh nhân (403).
8. Thời điểm trả về là UTC có hậu tố Z.
9. Schema chỉ do EF Core migration trong `backend/src/Database` tạo. Mapping của từng module khớp từng cột (kiểu, độ dài, null) với database do migration sinh; đổi mô hình mà quên tạo migration thì test báo lỗi.

## Thiết kế đã chọn
- Hai module theo Clean Architecture, mỗi tầng một project: Domain, Application, Infrastructure, Presentation (+ Identity.Contracts).
- Patients chỉ biết Identity qua `Identity.Contracts`; code Patients không đọc bảng AppUser.
- EF Core migration (chủ repo chọn ngày 29/10/2026, thay cho database-first): project `backend/src/Database` có `SchemaDbContext` mô tả đủ 46 bảng trong `dbo` dạng property bag và migration `InitialSchema`. Context này chỉ dùng để migrate. Mỗi module vẫn có DbContext riêng để đọc ghi, chỉ map bảng của mình.
- Không tự migrate khi khởi động (DB-03): API không tham chiếu `SmartHealthcare.Database`, có architecture test kiểm. Áp bằng `dotnet ef database update` hoặc script idempotent (xem `backend/src/Database/README.md`).

## Lệch so với ERD v2.2 và bộ luật (cần trưởng nhóm xác nhận)
- **Thêm** `Patient.RowVersion`. Giải pháp kỹ thuật v2.1 mục 1.1 yêu cầu sửa hồ sơ phải dùng rowversion, nhưng ERD v2.2 chưa có cột này.
- **MOD-01/MOD-04**: mọi module dùng chung schema `dbo` và một bộ migration chung (`SchemaDbContext` là context dùng chung, dù chỉ để migrate), không có schema và migration riêng theo module. Có FK giữa các module (ví dụ `PatientRepresentative.AppUserId → AppUser`); database-module-boundaries.md yêu cầu ADR cho FK xuyên module. Chưa có ADR.
- Mô hình bảng khai báo hai nơi: `SchemaDbContext` và DbContext của module. Test `Ef_mapping_matches_every_mapped_column_in_the_database` bắt lệch.
- `Staff.StaffType` chỉ có 4 giá trị theo ERD, **chưa có Admin**.
- Database `SmartHealthcare` cũ (4 bảng do migration tạo) không còn dùng; chưa xóa, chờ chủ repo quyết định.

## next_action
1. Trưởng nhóm review diff và các điểm lệch ở trên; viết ADR cho bộ migration chung trong `dbo` và FK xuyên module nếu đồng ý; chốt có cho Claude Code tiếp tục làm backend hay giao lại Codex.
2. Antigravity phản hồi contract v0.1 qua handoff; khi đồng ý thì chuyển Approved.
3. Task kế tiếp đề xuất: idempotency theo DB-04 (bảng `ApiIdempotencyRecord`), refresh token, xác minh email/số điện thoại, forwarded headers sau Nginx cho giới hạn tần suất.
