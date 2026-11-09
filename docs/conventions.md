# Quy ước code và API — Smart Healthcare

- Phiên bản: v0.1, Draft chờ trưởng nhóm duyệt (W1-03)
- Đối chiếu: code trên `dev` e714206; Giải pháp kỹ thuật v2.13 §2.3, §8.2; luật API-01..05, MOD-01..05, MSG-02, MSG-03 trong `docs/ai/instructions/`.
- Phạm vi: Backend .NET và contract trong `docs/api/`. Quy ước UI nằm ở `docs/ai/instructions/frontend-react.md`.

Khi quy ước này mâu thuẫn với Giải pháp kỹ thuật hoặc Phạm vi nghiệp vụ thì hai tài liệu đó thắng; sửa file này theo GOV-06.

## 1. Năm project của một module

Mỗi module `<M>` nằm trong `backend/src/Modules/<M>/` gồm 5 project:

| Project | Chứa | Được tham chiếu |
|---|---|---|
| `<M>.Contracts` | `I<M>Module`, DTO `record`, enum lỗi, integration event | Chỉ BCL (`System.*`) và `Shared.Abstractions` |
| `<M>.Domain` | Entity, enum trạng thái, bất biến | Không tham chiếu module khác |
| `<M>.Application` | Use case, application service, handler sự kiện, port (`I<...>Store`) | `<M>.Contracts`, `<M>.Domain`, `Shared.Abstractions`, `Contracts` của module khác. Không EF Core, không ASP.NET Core |
| `<M>.Infrastructure` | `<M>DbContext`, cấu hình EF, migration, cài đặt port | `<M>.Application`, `<M>.Domain`, `Shared.Infrastructure` |
| `<M>.Api` | Endpoint (Minimal API), DI của tầng Api | `<M>.Application`, `<M>.Contracts` |

- Module khác chỉ được tham chiếu `<M>.Contracts`. Không tham chiếu Domain, Infrastructure hay DbContext của nhau (MOD-01, Giải pháp §8.2). `ArchitectureTests` kiểm các luật này.
- Contract của một module **không** tham chiếu Contracts của module khác. Cần kiểu chung thì dùng kiểu BCL (Guid, DateTime, decimal, string) hoặc đặt trong `Shared.Abstractions`.
- Mỗi project có `DependencyInjection.cs` với `Add<M><Tầng>()`; tầng Api có thêm `Map<M>Endpoints()`. Bootstrapper (`SmartHealthcare.Api`, `SmartHealthcare.Worker`) chỉ gọi các hàm này.
- Test của module ở `backend/tests/<M>.Tests/` và phải có trong `SmartHealthcare.slnx`. Test chạm SQL Server ở `backend/tests/SqlServerTests/`.

## 2. Tên module, schema và DbContext

Tên DbContext và schema của ba module **khác tên module**:

| Module (thư mục, route) | DbContext | Schema |
|---|---|---|
| IdentityPatients | `IdentityPatientsDbContext` | `identity` |
| Scheduling | `SchedulingDbContext` | `scheduling` |
| Clinical | `ClinicalDbContext` | `clinical` |
| MedicationCatalog | **`MedicationDbContext`** | `medication` |
| Billing | `BillingDbContext` | `billing` |
| Notifications | **`WorkflowDbContext`** | `workflow` |
| DocumentsReporting | **`DocumentsDbContext`** | `documents` |
| Shared (hạ tầng) | `SharedDbContext` | `shared` |

- Mỗi DbContext đặt `HasDefaultSchema(Schema)` và bảng lịch sử migration `__EFMigrationsHistory` trong schema của nó.
- Tham chiếu chéo schema chỉ bằng ID, không FK; ghi chú `// ID chéo module: <schema>.<Bảng>, không FK` trên thuộc tính.
- `DateTime` lưu UTC (`UseUtcDateTime()`), tên thuộc tính kết thúc bằng `Utc`. Tiền là `decimal`, VND.

## 3. Endpoint HTTP

- Tiền tố: `/api/<module>` viết kebab-case: `/api/identity-patients`, `/api/scheduling`, `/api/clinical`, `/api/medication-catalog`, `/api/billing`, `/api/notifications`, `/api/documents-reporting`. Mỗi module có một route group gắn tag OpenAPI là tên module.
- Tài nguyên: danh từ số nhiều, kebab-case (`/doctor-days`, `/appointments`). Tham số đường dẫn camelCase có ràng buộc kiểu (`{doctorId:guid}`). Ngày phục vụ dạng `yyyy-MM-dd` (`DateOnly`).
- Hành động không phải CRUD: `POST` tới tài nguyên con là động từ (`POST .../{serviceDate}/close`, `POST /appointments/{id}/cancel`).
- Endpoint quản trị đặt dưới `/admin/...` và gắn policy `AdminOnly`. Endpoint nhân viên dùng policy `StaffOnly` hoặc policy theo vai trò (`Roles` trong `IdentityPatients.Contracts`).
- Mỗi endpoint khai báo **kênh** trong contract: `Staff` (web nhân viên) hoặc `Patient` (web bệnh nhân); callback cổng thanh toán là kênh hệ thống có xác minh chữ ký (API-05). Mỗi endpoint có test từ chối kênh sai.
- JSON camelCase (mặc định ASP.NET Core). Enum trả dạng chuỗi tên. Thời điểm là ISO-8601 UTC có hậu tố `Z`, tên trường kết thúc bằng `Utc`; hiển thị theo Asia/Ho_Chi_Minh ở UI (API-04).
- Danh sách có phân trang: query `page` (từ 1) và `pageSize`; contract ghi mặc định và giới hạn trên. Response gồm `items`, `page`, `pageSize`, `totalCount`.
- Lệnh thay đổi có thể bị gửi lặp nhận header `Idempotency-Key` (Giải pháp §2.3, §4). Cùng khóa + cùng nội dung trả lại kết quả cũ; khác nội dung trả 409 `IdempotencyConflict`; đang xử lý trả 409 `RequestInProgress` (hoặc 202 + `operationId` với lệnh dài).
- Tạo mới trả 201 kèm body; lệnh không có dữ liệu trả 204; lệnh dài đã tiếp nhận bền trả 202 + `operationId`.
- Endpoint chưa xong không được lộ ra: chưa gọi trong `Map<M>Endpoints()` hoặc chỉ map ở Development (GOV-05).

## 4. Lỗi và ProblemDetails

- Lỗi HTTP trả `application/problem+json` (RFC 9457) qua `TypedResults.Problem(...)`, kèm extension **`code`** là tên mã lỗi dạng PascalCase. Frontend và chatbot rẽ nhánh theo `code`, không theo `title` (title là tiếng Việt cho người đọc, có thể đổi).

  ```json
  { "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10", "title": "Ngày trực đã có giữ chỗ hoặc lịch hẹn.", "status": 409, "code": "HasBookings" }
  ```

- Ánh xạ mã trạng thái:

  | Status | Khi nào | Ví dụ `code` |
  |---|---|---|
  | 400 | Dữ liệu vào sai định dạng hoặc thiếu | `InvalidInput` |
  | 401 / 403 | Chưa đăng nhập / sai vai trò, sai kênh, không có quyền trên hồ sơ | do middleware xác thực trả |
  | 404 | Không có tài nguyên hoặc người gọi không được biết nó tồn tại | `DayNotFound`, `DoctorNotFound` |
  | 409 | Xung đột trạng thái hoặc quy tắc nghiệp vụ (hết suất, quá cutoff, đã tồn tại, rowversion cũ, idempotency) | `AlreadyExists`, `HasBookings` |
  | 503 | Dịch vụ phụ thuộc tạm ngưng (cổng thanh toán, chatbot ngoài giờ demo) | `GatewayUnavailable` |

- Không trả stack trace hay thông điệp exception ra client. Lỗi không lường trước đi qua `UseExceptionHandler()` thành 500 ProblemDetails.
- Không đưa CCCD, số điện thoại, triệu chứng hay kết quả vào `title`/`detail` (SEC-02).
- Lỗi giữa các module (gọi `I<M>Module`): enum `<M>ErrorCode` và `<M>Exception(code, message)` trong `<M>.Contracts` (mẫu: `BillingErrorCode`, `BillingException`). Module gọi tự ánh xạ sang ProblemDetails của mình khi cần trả ra HTTP. Kết quả thường gặp có thể trả enum/record thay cho exception (mẫu: `DoctorDayResult` của W2-01).
- Mã lỗi là một phần của contract: thêm mã thì ghi vào `docs/api/<module>.md`; đổi tên hoặc bỏ mã là breaking change (API-02).

## 5. Integration event

- Tên sự kiện là cụm danh từ + động từ quá khứ, PascalCase, **không** có hậu tố `Event`: `PaymentVerified`, `TicketCheckedIn`, `EncounterOpened`, `ResultPublished`, `AppointmentConfirmed`, `AppointmentCancelled` (Giải pháp §8.2).
- Tên lớp phải trùng một giá trị của `Shared.Infrastructure.Persistence.Enums.EventType`: `IIntegrationEventPublisher` lấy `EventType` từ tên lớp và tra `AggregateType` theo bảng cố định. Thêm sự kiện mới cần thêm giá trị enum và ánh xạ trong `IntegrationEventPublisher`.
- Đặt trong `<M>.Contracts` của module phát. Dạng `sealed record` hiện thực `IIntegrationEvent` theo mẫu `Billing.Contracts.PaymentVerified`: tham số đầu là `EventId`, có `OccurredAtUtc`, `OperationId` (nullable), `public const int CurrentVersion = 1` và `Version => CurrentVersion`.
- Payload tối thiểu, chỉ ID và giá trị mà bên nhận cần để quyết định; không bệnh án, không CCCD, không số điện thoại (MSG-03).
- Đổi payload không tương thích thì tăng `CurrentVersion`; bên nhận xử lý được cả bản cũ trong thời gian chuyển đổi.
- Phát bằng `IIntegrationEventPublisher.PublishAsync(e, aggregateId, ct)` bên trong `IUnitOfWork`, cùng transaction với thay đổi nghiệp vụ (MSG-02).
- Handler đặt trong `<M>.Application`, tên `<Sự kiện>Handler` (ví dụ `PaymentVerifiedHandler`). Handler gọi `IProcessedEventStore.TryMarkAsync(e.EventId, nameof(<Handler>), ct)` đầu tiên trong cùng unit of work; `false` thì bỏ qua.

## 6. Contract trong tiến trình (`I<M>Module`)

- Một interface `I<M>Module` cho mỗi module, method `<Động từ><Danh từ>Async(<Command|Query> request, CancellationToken ct)`. Request và response là `sealed record`; danh sách dùng `IReadOnlyList<T>`.
- XML doc của mỗi method ghi: bên gọi, điều kiện trước, khóa idempotency (gọi lặp trả gì), lỗi có thể trả, và có chạy trong `IUnitOfWork` của bên gọi hay không.
- Method chạy trong unit of work của bên gọi thì không tự mở transaction, không gọi dịch vụ ngoài (cổng thanh toán, R2, model).
- Contract chỉ chứa DTO và ID, không lộ entity hay `IQueryable` (MOD-02).
- Tài liệu contract ở `docs/api/<module>.md` (in-process + HTTP), có `contract_version` và trạng thái Draft/Approved; trưởng nhóm duyệt (H-02) trước khi tích hợp (API-01, API-02).
