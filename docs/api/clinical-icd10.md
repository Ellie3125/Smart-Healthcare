# ICD-10 lookup — schema gate

- contract_version: v0.1 Draft,18/11/2026.
- Route đề nghị: GET `/api/clinical/staff/icd10?q=`; **chưa triển khai/map**.
- Kênh dự kiến Staff/DoctorOnly; không mở API danh mục khi chưa có schema và dữ liệu đã duyệt.

ERD v2.4 và ClinicalDbContext hiện không có bảng danh mục ICD-10. `clinical.EncounterDiagnosis.ICD10Code`/`DiagnosisName` lưu chẩn đoán của từng lượt khám; không lấy dữ liệu bệnh án làm danh mục tra cứu.

Cần owner thiết kế bảng danh mục (mã unique,tên,trạng thái,nguồn/version), index cho tra mã/tên, migration mới và nguồn dữ liệu ICD-10 được duyệt. Cách nạp seed chưa có: chờ nguồn và phiên bản được duyệt, không tự tạo dữ liệu y khoa. Khi có schema sẽ chốt query/prefix/sort/tối đa20 kết quả và test SQL/phân quyền.

| Web đang gọi | Trạng thái / hành động |
|---|---|
| GET `/api/clinical/staff/icd10?q=` | Chờ schema; giữ nhập tay mã+tên như hiện tại; không gọi route này cho tới khi API được map |

Không cần sửa Bootstrapper trong đợt này. Không tạo migration hoặc đổi định dạng ICD-10/luồng draft,publish,amend đang chạy. Build/test backend không xác nhận UI đã bỏ lời gọi lookup.
