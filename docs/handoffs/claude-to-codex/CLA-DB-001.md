# Handoff CLA-DB-001 — Claude Code đã dựng tầng persistence trong vùng của Codex (OWN-10)

Trưởng nhóm giao trong chat ngày 29/10/2026 việc dựng database từ ERD v2.2 bằng EF Core code-first. Claude Code đã sửa các đường dẫn thuộc Codex dưới đây trên branch `feat/db-erd-v2.2` (chưa push). Đọc diff và `docs/db/erd-v2.2-implementation.md` trước khi làm tiếp; không revert khi chưa hỏi trưởng nhóm.

## File đã tạo/sửa trong vùng Codex
- `backend/src/Modules/{Identity,Catalog,Scheduling,Clinical,Billing,Workflow}/{Domain,Infrastructure}/**`: entity, enum, `IEntityTypeConfiguration<T>`, DbContext theo schema, design-time factory, migration `InitialCreate_ErdV22`. Catalog và Workflow là thư mục mới theo ERD. Đã xóa `.gitkeep` trong Domain/Infrastructure của Identity, Scheduling, Clinical, Billing.
- `backend/src/Shared/Persistence/**` (UtcDateTimeConverter, HasEnumCheck, SqlServerOptions), `backend/src/Tools/DbMigrator/**`, `backend/SmartHealthcare.slnx`, `backend/Directory.Build.props`.
- `tests/backend/Database.Tests/**`: test đối chiếu ERD và test ràng buộc trên SQL Server thật.
- `Directory.Packages.props` (EF Core 10.0.14, xunit.v3 4.0.2, ghim Pkcs 10.0.14), `global.json` (SDK 10.0.401, test runner MTP), `.config/dotnet-tools.json` (dotnet-ef 10.0.14), `.gitignore` (bin/, obj/).
- `docs/db/**` (ERD nguồn, script SQL, grants, báo cáo, sơ đồ), `docs/plans/db-erd-v2.2.md`, `docs/ai/ownership.md` (thêm docs/db/**, docs/plans/**, .config/** cho Codex).

## Việc Codex cần làm
1. Cập nhật Giải pháp kỹ thuật v2.2 §8.1.3/§8.1.5 cho khớp ERD đã chốt (hoặc đề xuất sửa ERD với trưởng nhóm): bảng giá ở schema `catalog` thay vì Billing; MedicationCatalog gộp vào `catalog`; `workflow` sở hữu BookingAttempt, Operation, ApiIdempotencyRecord, OutboxMessage, AuditLog; tên bảng quota là DoctorSessionSlotQuota.
2. Hạ tầng: tạo login/user migrate và runtime, gán vào role `sh_migrator`/`sh_runtime` theo `docs/db/sql/grants.sql`; cấp `SH_MIGRATOR_CONNECTION` cho bước migrate trong Jenkins/release; không cho BE1/BE2 tự migrate.
3. Release: chạy `DbMigrator` (hoặc script idempotent bằng `sqlcmd -I`) trước khi khởi động BE; nhớ `-I`, thiếu nó filtered index lỗi 1934.
4. CI: chạy `dotnet test --project tests/backend/Database.Tests` với SQL Server cùng dòng (`SH_TEST_SQL_CONNECTION`), không dùng InMemory/SQLite (DB-06).
5. Khi viết repository/service: tham chiếu chéo module chỉ bằng ID (52 cột ở mục 4 báo cáo), gán DateTime với Kind Utc (converter coi Unspecified là UTC), tên schema `identity` luôn để trong ngoặc vuông.

## Acceptance criteria phía Codex
- Giải pháp v2.2 không còn mâu thuẫn với ERD v2.2 về sở hữu bảng.
- Pipeline có bước migrate riêng dùng tài khoản migrate; runtime chỉ có quyền DML.
- Test Database.Tests chạy xanh trong CI trên SQL Server thật.
