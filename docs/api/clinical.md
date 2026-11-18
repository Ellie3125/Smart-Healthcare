# Contract Clinical

- contract_version: v0.4 Draft, 18/11/2026 (lifecycle, kết quả nháp và publish CLS).
- Phạm vi bản này: nhập sinh hiệu, Start/Finish CLS, xác minh vé thật, draft/publish kết quả và quyền đọc nội dung CLS.
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

## 5. Vòng đời CLS — contract trong tiến trình

`IClinicalModule.StartClsServiceAsync(ClsServiceCommand, ct)` và `FinishClsServiceAsync(ClsServiceCommand, ct)` do Scheduling gọi **bên trong UoW đang mở của caller**, sau khi cập nhật trạng thái vé tương ứng. Không tự mở transaction hoặc commit. `ClsServiceCommand(ServiceOrderItemId, RoomId, PerformedByUserId, OperationId?)`: caller đã xác minh phiên phòng, quyền và thanh toán; không lấy RoomId/actor từ body của bệnh nhân.

| Method | Điều kiện vé / item | Tác dụng |
|---|---|---|
| StartClsServiceAsync | Vé CLS thật đúng RoomId/item/Encounter, QueueStatus InProgress; order Confirmed, Encounter chưa đóng; item Ordered/SampleCollected | Item InProgress, audit metadata; đang InProgress thì no-op |
| FinishClsServiceAsync | Vé CLS thật đúng RoomId/item/Encounter, QueueStatus Finished; item InProgress | Item ServiceFinished + CompletedAtUtc giờ DB; tạo đúng một CLSResult Draft với nội dung rỗng và PerformedAtUtc, để Documents có ResultId thật trước upload. Item ServiceFinished/Completed thì no-op |

Khóa Encounter rồi ServiceOrderItem bằng UPDLOCK/HOLDLOCK trong transaction của caller. Giờ DB đọc sau khóa. Caller lỗi sau lời gọi sẽ rollback cả trạng thái Scheduling, item/result và audit. Không có migration, không tự đặt Published hoặc phát ResultPublished khi Finish.

Lỗi in-process là ClinicalException: InvalidRequest (ID rỗng), OrderItemNotFound (thiếu vé/item), Forbidden (sai phòng hoặc Encounter của vé không khớp), EncounterClosed, InvalidStatusTransition (vé/order/item sai trạng thái). Thiếu transaction Clinical participant ném InvalidOperationException, không có thay đổi. Default interface methods chỉ phục vụ fake cũ; bản host ClinicalInfrastructure nối writer thật qua factory, không mặc định thành công.

## 6. Vé CLS thật cho Documents / publisher

Scheduling công bố IClsTicketReader.GetByIdAsync/GetByItemAsync, trả ClsTicketContext(TicketId, EncounterId, ServiceOrderItemId, RoomId, QueueStatus, TicketNumber), null nếu thiếu hoặc không phải vé CLS. Chỉ query schema Scheduling theo PK/index item.

ISchedulingModule.ValidateClsTicketForRoomAsync hiện đọc ticket thật, kiểm RoomId, rồi qua Clinical read contract lấy Encounter.PatientId và CLSResult.Id đúng item. Chưa có result Draft/thiếu dữ liệu/sai phòng trả null. IsServiceFinished chỉ true khi QueueStatus Finished và item ServiceFinished/Completed; IsResultReturned theo CLSResult Published. PatientName/DateOfBirth null khi không có nguồn Identity thích hợp. Bỏ fallback cấp ID ngẫu nhiên/dữ liệu giả; host thiếu wiring cũng từ chối. Cần owner cập nhật docs/api/scheduling.md (lõi riêng).

## 7. Kết quả nháp và publish CLS

Cả ba route map ở Production và Development, RoomAccountOnly/Staff. Actor lấy từ sub và room_id của JWT, áp validator phiên thiết bị hiện có. Không nhận PatientId, StaffId hoặc tên bác sĩ làm bằng chứng quyền.

| Method / route | Request | Response |
|---|---|---|
| GET /api/clinical/staff/room/results/{orderItemId} | Không body | Draft envelope bên dưới |
| PUT cùng route | {summaryText,technicalConclusion?,version,readingDoctorId?} | Draft envelope mới, version mới |
| POST cùng route + /publish | {version,readingDoctorId?}, một Idempotency-Key | Receipt {resultId,orderItemId,resultStatus:"Published",version,publishedAtUtc}; không chứa nội dung |

Draft envelope: {resultId,orderItemId,queueTicketId,resultStatus:"Draft",summaryText,technicalConclusion,version,readingDoctorId?,readingDoctorName?,readingDoctors:[{doctorId,fullName}]}. Không chứa tệp, tên/ngày sinh bệnh nhân hoặc StaffId. queueTicketId do server suy từ orderItemId; dùng để gọi API Documents hiện có.

version là chuỗi base64 của rowversion SQL Server đúng8byte, KHÔNG là JSON number. summaryText trim, bắt buộc1..4000 UTF-16 code units; technicalConclusion trim, rỗng được, tối đa16000. Lưu vào ResultSummary và DetailedFindings. PUT chỉ cập nhật Draft bằng version hiện hành, không ghi revision/Outbox.

Vé CLS phải thuộc phòng, QueueStatus Finished, item ServiceFinished, order Confirmed, Encounter chưa Completed/Abandoned. Bác sĩ đọc lấy từ IIdentityPatientsModule.GetRoomReadingDoctorsAsync(roomId,atUtc): một query danh sách Doctor/Staff/AppUser/Room active, StaffType Doctor, phòng CLS, StaffShiftAssignment đúng ngày Việt Nam và StartsAtUtc<=giờDB<EndsAtUtc, gộp bác sĩ trùng. Một bác sĩ thì auto chọn; nhiều bác sĩ phải chọn DoctorId trong danh sách. Draft lưu StaffId đã xác minh vào ApprovedByStaffId; publish kiểm lại bác sĩ vẫn trong ca, đã hết ca phải chọn lại. ApprovedAtUtc và PerformedByStaffId không dùng theo ERD, không thêm bước duyệt.

Publish mở IUnitOfWork: idempotency24h scope AppUser + Clinical.PublishClsResult → khóa Encounter/item/result → kiểm version/trạng thái/ca → kiểm tệp hiện có đã Uploaded và có UploadedAtUtc → CLSResult Published + PublishedAtUtc giờDB, item Completed, revision1 Published chứa snapshot nội dung → Documents.MarkClsResultFilesPublishedAsync → audit metadata → ResultPublished Outbox → receipt idempotency; commit một lần. Lỗi publisher/caller rollback cả revision/result/item/files/audit/idempotency. Không gọi R2 trong transaction. Audit hạ tầng không sao chép nội dung kết quả.

Cùng key/nội dung trả đúng receipt cũ, kể cả lượt đã đóng, không xem lại nội dung. Cùng key khác nội dung trả409 IdempotencyConflict. Key mới sau Published trả409 ResultAlreadyPublished. Không đổi Draft/sửa nội dung Published qua các route này. Key1..128 ASCII hiển thị, không khoảng trắng; version chuẩn hóa base64 trước hash.

| HTTP | code / trường hợp |
|---|---|
| 400 | InvalidInput: JSON/null/version/key/độ dài sai, ID rỗng hoặc chọn bác sĩ ngoài danh sách/thiếu lựa chọn khi có nhiều bác sĩ |
| 401 | Anonymous/actor không hợp lệ/phiên phòng thật bị vô hiệu hoặc thiếu token phiên |
| 403 | Sai role/channel, thiếu room_id, vé khác phòng hoặc Encounter không khớp |
| 404 | ResultNotFound: thiếu vé/item/result |
| 409 | EncounterClosed, ServiceNotFinished, OrderNotActive, ResultAlreadyPublished, ConcurrencyConflict, ReadingDoctorUnavailable, FilesNotReady, IdempotencyConflict, RequestInProgress |
| 415 | UnsupportedMediaType |

### 7.1 Đọc nội dung đã phát hành

GET service-orders cho bác sĩ yêu cầu DoctorId đúng OriginalDoctor; chỉ trả Result Published, ẩn Draft/Cancelled. Nurse chỉ nhận metadata, Result=null. Route room/encounters/{id}/service-orders cũ giới hạn item theo vé đúng phòng bằng reader batch Scheduling; mọi Result=null, nội dung Draft chỉ đọc từ route mới theo orderItemId. Bệnh nhân phải có ClinicalRecords access (Verified), chỉ nhận Result Published sau IsPostClsConclusionPublishedAsync=true. Không dùng quyền Booking để xem bệnh án. Bác sĩ ban đầu xem Published ngay; phòng không xem lại sau publish.

### 7.2 Event và phần cần phối hợp trước chạy xuyên module

Canonical Clinical.Contracts.ResultPublished v1 phát mỗi item: EventId,EncounterId,ServiceOrderId,CLSResultId,ServiceOrderItemId,OccurredAtUtc,OperationId,Version. Tên CLR khớp EventType.ResultPublished, AggregateType CLSResult/AggregateId resultId; không dùng tên lớp cũ dài vượt cột EventType. Payload chỉ IDs/time, subscriber phải đọc lại readiness của cả đợt và chống replay/out-of-order.

Worker registration/consumer chưa đổi (lõi riêng): phối hợp đăng ký ResultPublished và handler W3-06 sau khi rebase; không bật luồng demo trước khi consumer thật sẵn sàng. #125/#128 chỉ hoàn wiring writer thật/rebase/test và đưa ra khỏi Draft **sau CLN-04 merge**. Không tự merge.

Documents hiện tạo metadata Draft trước R2 rồi cập nhật Uploaded; precheck ở Clinical chặn file đang tải đã nhìn thấy, chưa loại được upload mới chen giữa precheck/commit. Chủ Documents cần guard atomic ở bước ghi metadata/finalize và publish files để không thêm/ghi đè metadata sau Result Published, không phát hành file chưa Uploaded, không giữ transaction khi stream R2. Chưa sửa module Documents trong scope này; đây là điều kiện phối hợp trước nghiệm thu upload/publish đồng thời.

### 7.3 Ánh xạ cho Hoàng

| Web hiện tại | Contract / việc cần sửa |
|---|---|
| GET/PUT room/results/{orderItemId}, POST .../publish | Route đã có; version đổi number→base64 string; PUT trả Draft, POST trả receipt và UI khóa xem lại |
| readingDoctorName tự nhập, availableShiftDoctors:string[] | Chọn readingDoctorId từ readingDoctors[{doctorId,fullName}]; tên chỉ hiển thị, không dùng để ghi StaffId |
| draftFiles trong Clinical DTO | Đọc GET /api/documents/staff/room/results/{queueTicketId}/files riêng |
| Upload Documents bằng orderItemId | Cùng route hiện có nhưng path ID là queueTicketId do Clinical trả; giữ multipart/file và header theo contract Documents |
| ticketNumber/patientName/serviceCode/serviceName trong Draft DTO giả | Không có trong response mới; ghép context hàng chờ được phép, không giả dữ liệu trên server |

Không sửa frontend; UI/E2E/R2 thật chưa kiểm. Sửa/gỡ kết quả Published cần task riêng, schema revision đã có nhưng chưa triển khai trong bản publish này.
