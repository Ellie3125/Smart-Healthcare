# Contract Clinical

- contract_version: v0.3 Draft, 18/11/2026 (phần chỉ định CLS; tích hợp phần bệnh án v0.2 sau review riêng).
- Phạm vi bản này: nhập sinh hiệu và tạo đợt chỉ định CLS. Không sửa luồng tiền Billing hoặc schema.
- Căn cứ: giao việc CLN-01 ngày 17/11/2026; ERD v2.4 với bổ sung RespiratoryRate/NurseNote qua PR #123; Phạm vi nghiệp vụ v2.12; Giải pháp kỹ thuật v2.14; conventions.md.
- Trạng thái tích hợp: map trong `MapClinicalEndpoints`, cả Production và Development. `IdentityNurseStaffDirectory` gọi reader `GetActiveStaffAsync` đã có qua PR #126, chỉ nhận StaffType=Nurse. HTTP test có cả directory giả để cô lập transport và luồng Production với Identity/Clinical/audit/idempotency trên SQL Server thật.

## 1. Ghi một lần đo

`POST /api/clinical/staff/nurse/encounters/{encounterId:guid}/vitals`

Kênh **Staff**, policy **NurseOnly**. Ẩn danh 401; vai trò khác hoặc Nurse ở kênh Patient 403. Từ `sub`/NameIdentifier lấy AppUser.Id, tra Staff.Id của Nurse đang hoạt động qua Identity; thiếu hoặc không hợp lệ trả 403. Không lấy actor từ body và không dùng AppUser.Id thay Staff.Id.

Header `Content-Type: application/json`; một header `Idempotency-Key`, 1–128 ký tự ASCII hiển thị, không khoảng trắng. TTL 24 giờ, scope theo AppUser + `Clinical.RecordVitals`. Cùng khóa và cùng encounter/nội dung số đo trả nguyên kết quả 201 đã lưu; số bằng nhau như 70 và 70.00 được coi cùng nội dung. Khác nội dung 409 `IdempotencyConflict`; bản ghi còn đang chạy 409 `RequestInProgress`. Key mới tạo một lần đo mới. Replay sau khi lượt khám kết thúc chỉ trả kết quả cũ, không ghi thêm.

### Request

Các trường đều nullable hoặc có thể bỏ qua. Chỉ số dùng JSON number, ghi chú dùng string. Một lần đo thiếu chỉ số vẫn hợp lệ; tất cả null vẫn lưu được nếu điều dưỡng xác nhận gửi.

| Trường | Kiểu / đơn vị | Ngưỡng | Số chữ số thập phân tối đa |
|---|---|---|---|
| weightKg | decimal / kg | 0.5–500 | 2 |
| heightCm | decimal / cm | 20–260 | 1 |
| systolicBp | decimal / mmHg | 40–300 | 2 |
| diastolicBp | decimal / mmHg | 20–200 | 2 |
| heartRate | int / nhịp/phút | 20–250 | 0 |
| temperatureC | decimal / °C | 30–45 | 1 |
| spo2 | decimal / % | 40–100 | 2 |
| respiratoryRate | int / lần/phút | 4–80 | 0 |
| nurseNote | string | tối đa 1000 UTF-16 code units, theo nvarchar(1000) | — |

Có đủ hai chỉ số huyết áp thì tâm thu phải lớn hơn tâm trương. Không âm thầm làm tròn số trước khi ghi DB. Body không cần `encounterId`, `patientName`, `ticketNumber`, `recordedByStaffId`, `userId` hoặc `version`; các trường ngoài DTO không làm thay đổi định danh/actor.

```json
{
  "weightKg": 70, "heightCm": 175,
  "systolicBp": 120, "diastolicBp": 80,
  "heartRate": 72, "temperatureC": 36.5, "spo2": 98,
  "respiratoryRate": 16, "nurseNote": "Ghi chú giả lập"
}
```

### Response 201

`{ vitalSign, bmi }`, trong đó `vitalSign` là `VitalSignDto` hiện hữu: `id`, `encounterId`, `weightKg?`, `heightCm?`, `systolicBP?`, `diastolicBP?`, `pulseRate?`, `bodyTemperature?`, `spO2?`, `respiratoryRate?`, `nurseNote?`, `recordedByStaffId`, `recordedAtUtc`.

`bmi` là số thập phân một chữ số, tính bằng `BmiCalculator`/`BmiResult` khi có cả cân nặng và chiều cao; thiếu một trong hai thì null. `recordedAtUtc` là UTC ISO-8601 `Z`, lấy giờ DB sau khi khóa Encounter. Không RowVersion: VitalSign chỉ ghi thêm, bản hiện hành theo RecordedAtUtc mới nhất. Không sửa bản ghi trước đó.

### Lỗi

ProblemDetails có extension `code`, không đưa số đo/ghi chú hoặc exception vào title/detail. 401/403 từ auth có thể không có body.

| HTTP | code | Khi nào |
|---|---|---|
| 400 | InvalidInput | Body JSON sai/null, giá trị/precision không hợp lệ, Guid rỗng hoặc header idempotency thiếu/sai |
| 401 | — | Chưa đăng nhập hoặc token thiếu actor hợp lệ |
| 403 | — | Sai vai trò/kênh hoặc không xác minh được Nurse đang hoạt động |
| 404 | EncounterNotFound | Không có lượt khám |
| 409 | EncounterClosed | Lượt Completed hoặc Abandoned, yêu cầu ghi mới |
| 409 | IdempotencyConflict | Cùng key, khác encounter hoặc nội dung |
| 409 | RequestInProgress | Cùng key/nội dung còn đang xử lý |
| 415 | UnsupportedMediaType | Không gửi JSON |

## 2. Transaction và audit

`RecordVitalsService.RecordAsync(encounterId, staffId, userId, request, key, ct)` nhận Staff.Id đã xác minh ở tầng Api. Service mở IUnitOfWork, lấy idempotency trước rồi khóa PK Encounter bằng UPDLOCK/HOLDLOCK, đọc lại trạng thái, lấy giờ DB, ghi VitalSign + AuditLog + response idempotency trong cùng transaction. ClinicalDbContext phải là UoW participant (host Api hiện có). Lỗi bất kỳ bước nào rollback cả ba bản ghi. Hai backend cùng key không nhân đôi lần đo.

Audit: actor là AppUser.Id, RecordId là VitalSign.Id; NewValues chỉ chứa EncounterId/RecordedByStaffId, ChangedColumns nêu các cột đã ghi. Không sao chép số đo hoặc nurseNote vào audit hạ tầng. Không đổi hàng chờ, Encounter status hoặc phát Outbox khi lưu sinh hiệu.

## 3. Tích hợp Identity

Reader thực tế đã có qua PR #126: `IIdentityPatientsModule.GetActiveStaffAsync(Guid appUserId, CancellationToken ct)` trả `ActiveStaffDto?`, chỉ khi Staff và AppUser đang hoạt động. Adapter thuộc Clinical gọi reader này rồi kiểm `StaffType == "Nurse"`. Không sửa Identity hoặc suy Staff.Id từ body/JWT sub. Query Identity theo unique Staff.AppUserId và AppUser PK, không thêm schema/index trong thay đổi này.

Adapter đăng ký trong AddClinicalApi; MapClinicalEndpoints gọi MapNurseVitalsRoutes. Test Production kiểm cả trường hợp đang là Doctor nhưng token còn vai trò Nurse, Staff bị tắt, AppUser bị tắt: 403, kể cả replay key cũ. ClinicalDbContext đã được host Api đăng ký làm UoW participant; không cần sửa Bootstrapper.

## 4. Đối chiếu web nhân viên

| Web đang gọi | Contract / việc cần sửa ở web |
|---|---|
| PUT `/api/clinical/staff/nurse/encounters/{encounterId}/vitals` | Đổi POST, một lần đo một dòng; gửi số hoặc null, không chuỗi rỗng; bỏ version. Đọc response `{vitalSign,bmi}` và ánh xạ tên trường VitalSignDto như mục 1 |
| GET `/api/clinical/staff/nurse/queue` | Dùng GET `/api/scheduling/staff/rooms/{roomId}/queue?page=1&pageSize=50`, Scheduling v0.8 §4.6. Response envelope có items; item hiện không có EncounterId/patientName, cần nối context phù hợp |
| POST `/api/clinical/staff/nurse/queue/{encounterId}/call` | Dùng POST `/api/scheduling/staff/rooms/{roomId}/call-next`, body `{stationId?}`, §4.8; server chọn lượt tiếp theo |
| GET form sinh hiệu hiện tại | Chưa thuộc phần API mới của bản này; không tuyên bố luồng màn hình đã tích hợp |

Toast sau lưu phải phản ánh đã lưu sinh hiệu; thao tác này không tự chuyển vé sang hàng chờ bác sĩ. UI và E2E do người phụ trách web triển khai/kiểm chứng.

## 5. Tạo đợt chỉ định CLS

`POST /api/clinical/staff/doctor/encounters/{encounterId}/cls-orders`, Staff, DoctorOnly. AppUserId từ sub/NameIdentifier, DoctorId từ claim ký `doctor_id`; chỉ OriginalDoctor của Encounter. Không lấy actor/PatientId/tên/giá hoặc phòng từ body. Ẩn danh 401, sai vai trò/kênh/claim/bác sĩ 403.

JSON `{ "serviceIds": ["00000000-0000-0000-0000-000000000001"] }`; 1–50 ID khác rỗng, không trùng. `Content-Type: application/json`, một `Idempotency-Key` 1–128 ký tự ASCII hiển thị không khoảng trắng. Scope AppUser + Clinical.OrderCls, TTL 24h; cùng key và tập ID (không phụ thuộc thứ tự) replay 201; khác nội dung 409 IdempotencyConflict, đang xử lý 409 RequestInProgress. Quyền và Encounter chưa kết thúc kiểm trước replay.

Response 201: `{ order: ServiceOrderDto, tickets: ClsTicketsCreated }`. `order` chứa id, encounterId, orderRoundNumber, orderedAtUtc, prescribedByDoctorId, orderBatchStatus Confirmed, hasActiveReturnTicket false, items; item gồm id, serviceOrderId, medicalServiceId, serviceNameSnapshot, appliedPrice, itemStatus Ordered, isMandatory, completedAtUtc null, result null. `tickets` gồm clsTickets và returnTicket (Scheduling DTO). Tên và IsMandatoryDefault lấy qua IClsServiceDirectory của Scheduling, chỉ đọc theo batch ID đang hoạt động; appliedPrice từ ChargeDto.UnitPrice do Billing tự tra. Tiền VND, timestamp UTC `Z`. Vé quay lại Deferred, chưa active nên hasActiveReturnTicket false.

### Transaction

Khóa Encounter UPDLOCK/HOLDLOCK → kiểm OriginalDoctor/trạng thái → idempotency → đọc directory active → giờ DB → round MAX+1 của Encounter → tạo ServiceOrder Draft + ServiceOrderItem → IBillingModule.CreateClsChargesAsync (EncounterId, PatientId, ServiceOrderId, mỗi item quantity1) → ISchedulingModule.CreateClsTicketsAsync (vé CLS + một vé quay lại Deferred) → ghi snapshot giá, order Confirmed, Encounter WaitingCLS → audit + response idempotency. Tất cả cùng IUnitOfWork; Clinical/Billing/Scheduling/Shared phải là participant (host Api hiện có). Không gọi cổng thanh toán trong transaction. Bất kỳ lỗi Billing/Scheduling nào rollback cả order/items/fees/tickets/audit/idempotency; không giữ lại đợt Draft rác.

Audit chỉ EncounterId, round, DoctorId và số item. Không tạo migration hoặc FK chéo schema. Không thay đổi bộ đếm suất khám đầu; không tự kết thúc Encounter.

| HTTP | code | Điều kiện |
|---|---|---|
| 400 | InvalidInput | JSON/null, Guid hoặc danh sách dịch vụ không hợp lệ, thiếu/sai key |
| 401/403 | — hoặc Forbidden | Chưa đăng nhập / sai vai trò/kênh/claim/OriginalDoctor |
| 404 | EncounterNotFound | Thiếu lượt khám |
| 409 | EncounterClosed | Completed/Abandoned |
| 409 | MedicalServiceUnavailable | Dịch vụ thiếu/ngừng hoặc Scheduling không tìm được phòng hoạt động |
| 409 | IdempotencyConflict / RequestInProgress | Khóa xung đột / đang xử lý |
| 409 | PriceNotConfigured / AmbiguousPrice / DuplicateCharge | Billing từ chối cấu hình giá/khoản phí |
| 409 | InitialTicketNotFound | Scheduling không có vé khám đầu của Encounter |
| 409 | InvalidInput / InvalidRequest từ dependency | Scheduling/Billing từ chối command theo contract; transaction rollback |
| 415 | UnsupportedMediaType | Không gửi JSON |

### Hủy một mục CLS — yêu cầu còn thiếu

Chưa map lệnh hủy hoặc giả ghi Cancelled. IBillingModule chưa có hàm xử lý hủy theo ServiceOrderItemId (bao gồm phí chưa trả và khoản đã trả theo chính sách owner), ISchedulingModule chưa công bố lệnh hủy vé cho Clinical. Cần chủ contract chốt lệnh tham gia UoW, khóa tài chính/vé và idempotency; sau đó Clinical khóa Encounter/item, chỉ nhận item chưa thực hiện, đổi Cancelled và gọi hai module cùng transaction. Không tự sửa thu/hoàn tiền. Phần này còn chờ contract cho W3-13/W3-07.

### Ánh xạ web

Web `orderClsServices` dùng đúng POST route và body serviceIds/Idempotency-Key. Response là order+tickets, cần reload workspace; không phải DoctorEncounterWorkspaceDto. Route GET `/api/clinical/staff/doctor/cls-services` chưa thuộc việc này; danh mục thuộc Scheduling. Các API draft/publish/amend là PR độc lập; UI/E2E chưa kiểm chứng.

Scheduling công bố IClsServiceDirectory.GetAsync(IReadOnlyList<Guid>, ct) trả OrderableClsService(Id, Name, IsMandatory), 1–50 ID distinct nonempty; chỉ trả active, không ghi/không cache schema khác. Cần chủ docs/api/scheduling.md bổ sung mục contract này (lõi riêng), không sửa tài liệu lõi trong PR này.
