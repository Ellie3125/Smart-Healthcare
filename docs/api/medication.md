# MedicationCatalog — lookup thuốc

- contract_version: v0.1 Draft, 18/11/2026.
- `GET /api/medication-catalog/staff/drugs?q=`; Staff, policy DoctorOnly. Ẩn danh401; sai vai trò hoặc Doctor ở Patient403. Không thay prescription hoặc kê liều tự động.
- `q` tùy chọn; bỏ/trống/khoảng trắng trả tối đa20 thuốc active đầu tiên. Có q: trim rồi tìm prefix MedicationCode/MedicationName/ActiveIngredient theo SQL Server collation hiện tại. Giới hạn chuỗi gốc100 UTF-16 code units, không control character; vi phạm400 ProblemDetails code `InvalidInput`. Ký tự `%`, `_`, dấu nháy được EF parameterize/escape, không là SQL/wildcard từ client.
- Thứ tự MedicationName,MedicationCode,Id tăng dần; tối đa20, không pagination/totalCount. Không có kết quả trả200 `[]`. Chỉ AsNoTracking trong MedicationDbContext, không truy vấn module khác. Index unique MedicationCode hiện có; tìm tên/hoạt chất dùng schema hiện tại, không thêm index/migration.

Response là mảng `{id,name,activeIngredient,unit,defaultDosage,code,strength,dosageForm}`. `unit`: Vien/Goi/Chai/Ong; `dosageForm`: Tablet/Capsule/Liquid/Injection. `name` là MedicationName nguyên bản; `strength` là hàm lượng được cấu hình, không phải liều dùng. **defaultDosage luôn chuỗi rỗng** vì schema không có liều mặc định; bác sĩ tự nhập liều/đường dùng/tần suất theo workflow hiện hành, không suy từ hàm lượng. Web nên hiện code/strength/dosageForm để phân biệt thuốc cùng tên. Không có timestamp/tiền hoặc idempotency cho GET này.

```json
[{"id":"11111111-1111-1111-1111-111111111111","name":"Thuốc thử nghiệm","activeIngredient":"Hoạt chất thử nghiệm","unit":"Vien","defaultDosage":"","code":"TEST-001","strength":"10 mg","dosageForm":"Tablet"}]
```

| Web đang gọi | Backend thật / việc cần sửa |
|---|---|
| GET `/api/medication/staff/drugs?q=` | Đổi sang `/api/medication-catalog/staff/drugs?q=` theo root module hiện có; giữ response mảng và các trường DrugCatalogItem, dùng metadata bổ sung khi hiển thị |

Bootstrapper hiện đã gọi AddMedicationCatalogInfrastructure/Application/Api và MapMedicationCatalogEndpoints; không cần đăng ký module mới hay sửa Program.cs. Không tạo route alias. Không cần schema mới. Không có seed danh mục trong PR; database chưa nạp thuốc sẽ trả mảng rỗng, owner danh mục phải nạp dữ liệu đã duyệt trước demo, không sinh thuốc/liều giả từ bệnh án.

SQL integration qua WebApplicationFactory đăng ký module thật trong test, cùng transaction DB test rollback; kiểm active-only/prefix/sort/top20/input và401/403. Test-only Microsoft.AspNetCore.Mvc.Testing10.0.14 khớp Microsoft.Extensions.Hosting10.0.14 đã pin; version override trong project test để không sửa file lõi. Đề nghị owner thêm PackageVersion trung tâm rồi bỏ VersionOverride; không là blocker đăng ký host/runtime. UI/E2E chưa kiểm.
