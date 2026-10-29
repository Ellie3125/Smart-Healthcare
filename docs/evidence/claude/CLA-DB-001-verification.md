# Evidence CLA-DB-001 — dựng DB từ ERD v2.2

Output thật, đã loại mật khẩu. Ngày 29/10/2026, máy dev Windows 11, cwd `D:\ĐA\Smart-Healthcare`, branch `feat/db-erd-v2.2`.

## Môi trường
- .NET SDK 10.0.401; dotnet-ef 10.0.14 (local tool); EF Core 10.0.14.
- SQL Server: `mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` (tag 2022-CU27-ubuntu-22.04), log: `Microsoft SQL Server 2022 (RTM-CU27) (KB5104824) - 16.0.4295.3 (X64)`, edition Developer, container `sh-sqltest` chỉ mở cổng 127.0.0.1:14333. Mật khẩu sa sinh ngẫu nhiên cho phiên test, lưu ngoài repo.

## 1. Build sạch (đã xóa bin/obj)
Lệnh: `dotnet build backend/SmartHealthcare.slnx`
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
exit=0
```

## 2. Toàn bộ test trên SQL Server thật
Lệnh: `SH_TEST_SQL_CONNECTION="Server=127.0.0.1,14333;User Id=sa;Password=***;TrustServerCertificate=True" dotnet test --project tests/backend/Database.Tests`
```
SmartHealthcare.Database.Tests.dll (net10.0|x64) passed (11s 068ms)
  total: 17
  failed: 0
  succeeded: 17
  skipped: 0
exit=0
```
Gồm 7 test đối chiếu model (1 kiểm 46 bảng + 6 module) và 10 test ràng buộc SQL Server.

## 3. Test đỏ trước khi có cấu hình (TDD)
- identity, chỉ có DbContext rỗng: `9 sai lệch: Thiếu bảng AppUser ... Thiếu bảng RoomDeviceSession`, failed 1.
- identity, đã có config nhưng CHECK enum chưa đặt được tên bảng: `6 sai lệch: Staff.StaffType: thiếu CK_Staff_StaffType ...` → sửa bằng `ToTable(nameof(T), ...)` → xanh.
- catalog/scheduling/clinical/billing/workflow, chuyển thư mục Configurations ra ngoài project: lần lượt `7`, `8`, `10`, `7`, `5 sai lệch`, failed 1; trả lại → `total: 7, failed: 0`.
- Đột biến: đổi tên `CK_RefundRequest_PolicyRule` → `RefundRequest: thiếu CK_RefundRequest_PolicyRule`; Restrict→Cascade ở PaymentAllocation → `OnDelete Cascade ≠ Restrict` (2 cột). Bỏ HasFilter UK 1B → không đổi model (EF tự thêm filter IS NOT NULL cho unique index cột nullable).

## 4. Script idempotent chạy 2 lần trên DB trống
Lệnh (trong container): `sqlcmd -S localhost -U sa -C -b -I -d IdemTest2 -i /tmp/sql/<module>.sql`, thứ tự identity → workflow, chạy hai vòng.
```
lần 1 identity exit=0 ... lần 1 workflow exit=0
lần 2 identity exit=0 ... lần 2 workflow exit=0
grants exit=0
billing: 7 / catalog: 7 / clinical: 10 / identity: 9 / scheduling: 8 / workflow: 5
tong bang: 46
CHECK: 61
FK: 46
FK cheo schema: 0
filtered index: 7
```
Lần đầu chạy thiếu `-I`: identity/scheduling/billing exit=1 với `Msg 1934 ... SET options have incorrect settings: 'QUOTED_IDENTIFIER'` (sqlcmd mặc định QUOTED_IDENTIFIER OFF). Đã ghi lưu ý vận hành trong báo cáo.

## 5. DbMigrator
- Không có biến môi trường: `Thiếu biến môi trường SH_MIGRATOR_CONNECTION.` exit=1.
- Với `SH_MIGRATOR_CONNECTION` trỏ DB mới `MigratorTest`, chạy hai lần: mỗi lần in `Migrate IdentityDbContext...` → `Migrate WorkflowDbContext...` → `Xong.`, exit=0.

## 6. Quét bảo mật
- `gitleaks protect --staged --no-banner` trước mỗi commit (dbd0043, 5f5f3e9, 85b5977, 869f5e0, fb01e5f, f16f88c, 6042350, c013678, 67527e7): không lỗi. `gitleaks detect --no-git --source . --redact`: `no leaks found`.
- `semgrep scan --config p/csharp --config p/secrets --metrics=off --quiet --error backend/src tests/backend docs/db`: exit=0, 0 finding.
- Restore ban đầu báo NU1903/NU1902 cho `System.Security.Cryptography.Pkcs` 9.0.11 (GHSA-846f-f764-76mx high, GHSA-2fmv-rv2c-59x7 moderate) → ghim 10.0.14 qua CentralPackageTransitivePinning; restore sạch.

## 7. Sơ đồ Archify
`node archify.mjs finalize architecture ... docs/db/erd-v2.2-schemas.html --repo-root . --quality showcase`: gates validate/deliver/check/browser-check = pass; cảnh báo `i18n/locale-fallback` (giao diện viewer tiếng Anh vì không có catalog `vi`); 9 giao cắt đường nối (đồ thị gần đầy đủ giữa 6 schema), một lần thử bố cục 2×3 cho 10 giao cắt nên giữ bản đầu. Chưa có người xem trực quan.

## Chưa kiểm chứng
- Chạy `grants.sql` với user thật không phải sa (mới chạy bằng sa để kiểm cú pháp).
- Hiệu năng/index theo truy vấn thật (chưa có truy vấn).
- SQL Server bản Staging/Prod.
