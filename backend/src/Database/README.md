# SmartHealthcare.Database

Mô hình schema của cả 46 bảng ERD v2.2 (`dbo`) và các EF Core migration. Project này chỉ dùng để sinh và áp migration. API và các module không tham chiếu nó (DB-03). Module đọc ghi dữ liệu qua DbContext riêng của mình.

Chạy các lệnh dưới đây từ thư mục `backend/`. Lần đầu chạy `dotnet tool restore` để có `dotnet ef`.

## Thêm migration khi đổi schema
1. Sửa mô hình trong `Schema/*.cs`. Nếu module có map bảng đó, sửa luôn DbContext của module.
2. Sinh migration:
   ```
   dotnet ef migrations add <TenThayDoi> --project src/Database --startup-project src/Database --output-dir Migrations
   ```
3. Đọc file migration vừa sinh, rồi chạy `dotnet test` ở cả ba project test. `SchemaTests` báo lỗi nếu quên tạo migration, hoặc nếu DbContext của module lệch với bảng.

## Áp migration
- Máy dev:
  ```
  dotnet ef database update --project src/Database --startup-project src/Database --connection "Server=localhost;Database=SmartHealthcare_ERD;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"
  ```
- Môi trường dùng chung (DB-03): sinh script idempotent để review, rồi áp bằng một tác vụ riêng trong release:
  ```
  dotnet ef migrations script --idempotent --project src/Database --startup-project src/Database -o migrate.sql
  ```

Thay vì `--connection`, có thể đặt biến môi trường `SMARTHEALTHCARE_MIGRATIONS_CONNECTION`.
