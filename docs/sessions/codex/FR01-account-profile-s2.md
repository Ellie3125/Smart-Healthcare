# Session FR01-account-profile s2

| Trường | Giá trị |
|---|---|
| Công cụ | Claude Code (Opus 5.5), trong VS Code |
| Thời gian | 29/10/2026 |
| Branch / base / HEAD | feature/fr01-account-profile / 3d7030c / 3d7030c (thay đổi chưa commit) |
| Contract | docs/api/fr01-account-profile.md v0.1 Draft, API không đổi |

## Yêu cầu và quyết định
Chủ repo yêu cầu tạo database theo 7 sơ đồ ERD v2.2, sau đó "cập nhật FR01 theo database mới". Chủ repo chọn: **Database-first** (script là nguồn duy nhất của schema, bỏ migration) và **thêm `Patient.RowVersion`** vào script/database.

## Thay đổi
- Thêm `infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql`: 46 bảng trong `dbo`, kèm FK, CHECK, unique (có filtered) và index cho mọi cột FK. Script chỉ chạy được trên database trống.
- Database `SmartHealthcare_ERD` trên localhost tạo từ script; đã thêm `Patient.RowVersion`.
- `IdentityModuleDbContext`, `PatientsModuleDbContext`: map vào `dbo`, kiểu cột khớp script; bỏ default schema, bảng lịch sử migration, design-time factory, CHECK/index khai báo trong model.
- Xóa `Persistence/Migrations` của hai module, `backend/dotnet-tools.json`, gói `Microsoft.EntityFrameworkCore.Design` trong Api.
- `UserAccount.PhoneNumber` và `AccountView.PhoneNumber` không còn nullable (cột `NOT NULL`); `PhoneMaxLength` 20 → 15.
- Tests: `ApiFactory` tạo database tạm bằng script; SQL trong test đổi sang `dbo`; thêm `SchemaScriptTests` (đủ 46 bảng, mapping EF khớp từng cột).
- Api: thêm OpenAPI (`Microsoft.AspNetCore.OpenApi` 10.0.14) và Swagger UI (`Swashbuckle.AspNetCore.SwaggerUI` 10.3.1), chỉ bật ở Development; `/` chuyển tới `/swagger`, có nút Authorize cho JWT. Kiểm: `/` → 302, `/swagger/index.html` → 200, `/openapi/v1.json` đủ 6 path; architecture 20/20, integration 20/20; không có package lỗ hổng.
- Ngoài repo: user-secrets `ConnectionStrings:SmartHealthcare` trỏ `SmartHealthcare_ERD`.

## Lệnh và kết quả
Xem docs/evidence/codex/FR01-test-run.md, mục "Lần 2".

## Chưa làm / chưa kiểm chứng
- Lệch MOD-01/MOD-04 và DB-03 ghi trong task file; chưa có ADR.
- Database `SmartHealthcare` cũ còn trên máy dev, không còn dùng.

## next_action
Trưởng nhóm duyệt database-first và FK xuyên module (ADR); chủ repo quyết định có xóa database `SmartHealthcare` cũ không.
