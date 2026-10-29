# Evidence FR01-account-profile

Output thật, đã lược bớt dòng log. Không có dữ liệu bệnh nhân thật; dữ liệu đều giả lập.

## Lần 3: quay lại EF Core migration cho cả 46 bảng (29/10/2026)

Project `backend/src/Database` (`SchemaDbContext`, migration `20261029144917_InitialSchema`). Đã xóa script `infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql`.

| Bước | Lệnh / cwd | Kết quả |
|---|---|---|
| Sinh migration | `dotnet ef migrations add InitialSchema --project src/Database --startup-project src/Database --output-dir Migrations` (cwd `backend/`) | 46 CreateTable, 98 FK, 94 CHECK, 37 UNIQUE constraint, 88 index, 119 DEFAULT có tên |
| Áp vào DB tạm | `dotnet ef database update ... --connection "...Database=SmartHealthcare_MigrationCheck..."` | Applying `InitialSchema`, Done; collation `Vietnamese_100_CI_AS` |
| So với DB tạo từ script | `compare_migration.sql` (scratchpad) với `SmartHealthcare_ERD` cũ | 824 mục (cột, kiểu, null, tên PK/UQ/FK/CHECK/DEFAULT/index) khớp; chỉ khác bảng `__EFMigrationsHistory` |
| Tạo lại DB thật | Kiểm `SmartHealthcare_ERD` có tổng 0 dòng trên mọi bảng → DROP → `dotnet ef database update` | 46 bảng + `__EFMigrationsHistory`; `migrations list` hiện `InitialSchema` đã áp |
| Build | `dotnet build backend/SmartHealthcare.slnx` | exit 0, 0 warning |
| Unit | `dotnet test tests/backend/Unit/...` | exit 0, 21/21 |
| Architecture | `dotnet test tests/backend/Architecture/...` | exit 0, 21/21 (thêm test API không tham chiếu project migration) |
| SQL Server integration | `dotnet test tests/integration/...` | exit 0, 21/21; DB tạm tạo bằng `MigrateAsync`; không còn DB `SmartHealthcare_Test_%` |

- Test `Schema_model_has_no_changes_without_a_migration`: đổi thử `PasswordHash` sang `nvarchar(510)` mà không tạo migration thì test báo lỗi; đã hoàn tác.
- Smoke trên Kestrel (`http://localhost:5022`, DB `SmartHealthcare_ERD`): Swagger mở được; login tài khoản không tồn tại → 401 `invalid_credentials`.
- Chưa kiểm chứng: áp `migrations script --idempotent` bằng sqlcmd; chạy hai replica cùng lúc migrate (EF 10 có lock migration nhưng chưa thử).

## Lần 2: chuyển sang database-first (29/10/2026, đã thay bằng lần 3)

Schema lấy từ `infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql` (46 bảng, `dbo`). Đã xóa hai migration và gói `Microsoft.EntityFrameworkCore.Design`. Môi trường như mục bên dưới.

| Bước | Lệnh / cwd gốc repo | Kết quả |
|---|---|---|
| Build | `dotnet build backend/SmartHealthcare.slnx` | exit 0, 0 warning |
| Unit | `dotnet test tests/backend/Unit/SmartHealthcare.UnitTests.csproj` | exit 0, Passed 21/21 |
| Architecture | `dotnet test tests/backend/Architecture/SmartHealthcare.ArchitectureTests.csproj` | exit 0, Passed 20/20 |
| SQL Server integration | `dotnet test tests/integration/SmartHealthcare.IntegrationTests.csproj` | exit 0, Passed 20/20 (18 cũ + 2 test schema mới) |

- Integration test tạo database tạm rồi chạy chính script trong repo (tách theo `GO`). Sau khi chạy không còn database `SmartHealthcare_Test_%`.
- Test mới `Ef_mapping_matches_every_mapped_column_in_the_database` so kiểu, độ dài, null của từng cột EF map với database. Đã thử đổi `PasswordHash` sang độ dài 512: test báo lỗi `dbo.AppUser.PasswordHash: EF 'nvarchar(512) not null'…`. Đã hoàn tác.
- Database `SmartHealthcare_ERD`: chạy `ALTER TABLE dbo.Patient ADD RowVersion rowversion NOT NULL` (bảng đang 0 dòng). Tạo database `SmartHealthcare_SchemaCheck` từ script trong repo rồi so với `SmartHealthcare_ERD`: 824 mục (cột, kiểu, null, tên PK/UQ/FK/CHECK/DEFAULT/index), không có khác biệt. Database so sánh đã xóa.
- Smoke trên Kestrel (`dotnet run`, user-secrets trỏ `SmartHealthcare_ERD`), chỉ gọi endpoint không ghi dữ liệu: `/api/auth/me` không token → 401; login tài khoản không tồn tại → 401 `invalid_credentials`. Log không có lỗi. `dbo.AppUser` vẫn 0 dòng.
- Chưa kiểm chứng: đăng ký và hồ sơ trên chính `SmartHealthcare_ERD` (chỉ kiểm trên database tạm tạo từ cùng script).

## Lần 1: EF migration theo module

## Môi trường
- Windows 11, .NET SDK 10.0.401, target net10.0; EF Core 10.0.14; Microsoft.Data.SqlClient qua EF.
- SQL Server 2025 Enterprise Developer Edition, instance mặc định `localhost`, Windows Authentication, collation `Vietnamese_100_CI_AS`.
- Branch `feature/fr01-account-profile`, base `3d7030c`, chưa commit.

## Build
cwd `backend/`; `dotnet build SmartHealthcare.slnx` → exit 0, `Build succeeded`, 0 warning (TreatWarningsAsErrors bật).
`dotnet list SmartHealthcare.slnx package --vulnerable --include-transitive` → không project nào có package lỗ hổng. Đã ghim `System.Security.Cryptography.Pkcs` 10.0.14 vì bản 9.0.11 kéo theo qua SqlClient có GHSA-2fmv-rv2c-59x7 và GHSA-846f-f764-76mx.

## Test (cwd gốc repo, 29/10/2026 khoảng 13:50 UTC)

| Tầng | Lệnh | Kết quả |
|---|---|---|
| Unit | `dotnet test tests/backend/Unit/SmartHealthcare.UnitTests.csproj` | exit 0, Passed 21/21 |
| Architecture | `dotnet test tests/backend/Architecture/SmartHealthcare.ArchitectureTests.csproj` | exit 0, Passed 20/20 |
| SQL Server integration | `dotnet test tests/integration/SmartHealthcare.IntegrationTests.csproj` | exit 0, Passed 18/18 |

Integration test chạy API thật (WebApplicationFactory) trên database tạm `SmartHealthcare_Test_<guid>`, áp đủ migration rồi xóa khi xong. Sau khi chạy, không còn database test nào.

Các kịch bản tranh chấp đã chạy:
- 8 request đăng ký cùng email chạy song song: đúng 1 request trả 201, còn lại 409; DB có đúng 1 dòng.
- 6 request tạo hồ sơ Self chạy song song: đúng 1 request trả 201, còn lại 409; DB có đúng 1 liên kết Self.

## Migration
- `dotnet ef migrations has-pending-model-changes`: cả hai context báo "No changes have been made to the model since the last migration".
- Đã áp script idempotent vào DB `SmartHealthcare` sau khi xóa 35 bảng cũ (đã kiểm 0 dòng dữ liệu). Kết quả có `identity.AppUser`, `identity.Staff`, `patients.Patient`, `patients.PatientRepresentative`, mỗi schema một `__EFMigrationsHistory`. `dbo.sysdiagrams` được giữ.

## Smoke trên Kestrel
API chạy bằng `dotnet run` trên DB tạm `SmartHealthcare_Smoke`, gọi bằng curl qua HTTP:

| Bước | Kết quả |
|---|---|
| Gọi `/api/auth/me` không có token | 401 |
| register | 201 |
| login bằng tên đăng nhập | 200 |
| `/api/auth/me` | 200 |
| tạo hồ sơ Self | 201 |
| liệt kê hồ sơ | 200 |
| login sai mật khẩu | 401 |

- Tên và địa chỉ tiếng Việt gửi bằng file UTF-8 được lưu và đọc đúng.
- `createdAtUtc` lúc tạo và lúc đọc lại giống nhau, đều có `Z`.
- DB smoke đã xóa.

Lỗi phát hiện ở lần smoke đầu và đã sửa: thời điểm đọc từ DB mất hậu tố Z và lệch độ chính xác so với lúc tạo. Đã thêm test hồi quy `Timestamps_are_utc_and_identical_after_reload`.

## Chưa kiểm chứng
- Chưa chạy hai replica Backend thật sau Nginx; tranh chấp mới chỉ kiểm trong một tiến trình, nhưng do unique index của SQL Server quyết định.
- Giới hạn tần suất chưa có test tự động; sau Nginx cần forwarded headers mới lấy được IP thật.
- UI và E2E: Not run (thuộc Antigravity).
- Architecture test chưa được thử bằng cách cố ý tạo vi phạm để xem test bắt được hay không.
