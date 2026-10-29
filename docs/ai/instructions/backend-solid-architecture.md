# backend-solid-architecture

## Phạm vi
ASP.NET Core backend và tests kiến trúc.

## Khi nào đọc
Thiết kế module/use case, sửa dependency hoặc DI.

## Quy tắc
- **ARCH-01** — Backend dùng Modular Monolith ngay từ đầu, áp dụng Clean Architecture bên trong từng module. Toàn Backend nghiệp vụ là một ứng dụng triển khai thống nhất, có ít nhất ba module nghiệp vụ thực chất; mỗi module sở hữu dữ liệu/schema và public contract. Xem database-module-boundaries.md.
- **ARCH-02** — Mỗi module tách Contracts, Domain, Application, Infrastructure và Api (endpoint REST của module). Chiều phụ thuộc hướng vào Domain/Application; Contracts không phụ thuộc implementation. Bootstrapper/SmartHealthcare.Api là host/composition root lắp ghép module, Bootstrapper/SmartHealthcare.Worker là host Hangfire; không gom nghiệp vụ vào host. Không gọi chéo DbContext/Repository.
- **ARCH-03** — Áp dụng SOLID có lý do thay đổi: tách nghiệp vụ khỏi IO, interface theo nhu cầu client, thay implementation không phá contract. Không bắt buộc generic repository/base service/CQRS hay interface cho mọi class. Trước code chốt domain/ERD và UML cần thiết: use case, activity phân vai, sequence đặt lịch/chatbot/thanh toán/trả kết quả, class/domain, component, deployment và state machines; cập nhật theo implementation thật.
- **BE-01** — Controller xử lý transport/validation rồi gọi use case. Dùng DTO tách entity; nullable rõ; async IO và CancellationToken; DI lifetime đúng, không capture scoped DbContext trong singleton.
- **BE-02** — Giữ framework test/namespace của repo khi có; repo mới chốt bằng ADR. Không thêm Semantic Kernel/Azure/telemetry vì skill gợi ý. Pin SDK/EF provider tương thích; build reproducible.

## Cấu trúc Backend theo Giải pháp kỹ thuật v2.1 §8.2.2

| Đường dẫn | Vai trò |
|---|---|
| backend/src/Bootstrapper/SmartHealthcare.Api/ | Host Web API, composition root, middleware và nạp module (BE1, BE2 cùng image) |
| backend/src/Bootstrapper/SmartHealthcare.Worker/ | Host Hangfire Server, phát Outbox; xem background-jobs-outbox.md |
| backend/src/Modules/<Module>/<Module>.{Contracts,Domain,Application,Infrastructure,Api}/ | Các module theo bounded context |
| backend/src/Shared/Shared.Abstractions/ | IIntegrationEvent, IUnitOfWork, IEventPublisher; không shared entity/DbContext |
| backend/src/Shared/Shared.Infrastructure/ | Outbox, bảng ProcessedEvent, unit of work, dispatcher Hangfire |
| tests/backend/ArchitectureTests/ | Kiểm luật tham chiếu giữa các module |

Các module: Scheduling, Clinical, Billing, IdentityPatients, MedicationCatalog, Notifications (Workflow và Notifications) và DocumentsReporting; ranh giới, bất biến và lý do chia ở Giải pháp kỹ thuật v2.1 §8.1. Cây backend/src hiện có (Api/, SharedKernel/, Identity và Patients tách riêng, Documents và Reporting tách riêng, thư mục Presentation/, chưa có MedicationCatalog) là cấu trúc cũ chờ Codex chuyển theo handoff claude-to-codex/CLA-GOV-001.

Với mỗi module:
- <Module>.Domain: entity, value object, invariant và domain event; không phụ thuộc EF Core, ASP.NET Core hoặc tầng ngoài.
- <Module>.Application: use case, application service, event handler và port; phụ thuộc Domain và contract cần thiết, không phụ thuộc Infrastructure/Api.
- <Module>.Infrastructure: DbContext riêng (schema của module), repository/adapter hiện thực port; phụ thuộc hướng vào Application/Domain.
- <Module>.Api: endpoint REST của module, gọi Application; không thay thế Frontend.
- <Module>.Contracts: interface I<Module>Module, DTO và integration event; không expose EF entity, DbContext, repository, IQueryable hoặc kiểu nội bộ. Không tham chiếu implementation của module.

Không tạo một bộ Domain/Application/Infrastructure dùng chung cho toàn bộ nghiệp vụ. Module khác chỉ tham chiếu project Contracts, không truy cập trực tiếp schema/module data. Chỉ chia sẻ phần Shared tối thiểu có lý do.

Hai Backend replica chạy cùng image của một Modular Monolith; worker .NET là tiến trình/container riêng nhưng chưa phải microservice. Chatbot FastAPI là service Python nội bộ riêng, chỉ gọi REST API công khai của .NET; không tách mỗi module nghiệp vụ thành container/microservice. Chỉ tách module thành service sau này khi có nhu cầu rõ về vận hành, scale hoặc ranh giới đội ngũ và có thiết kế được chấp thuận.

Nếu nhiệm vụ hiện tại chỉ khởi tạo cây thư mục, tạo đúng thư mục rỗng; không tạo solution/project, class/interface, DbContext, migration, file mẫu hoặc .gitkeep. Việc có thư mục chưa chứng minh module boundary hoặc dependency đã được kiểm chứng.

## Điều cấm
Không expose EF entity trực tiếp làm API, không sync-over-async, không hard-code quyền trong UI. Không sinh abstraction, base class hoặc tầng trung gian không có trách nhiệm thực chất. Không biến cấu trúc thư mục thành bằng chứng code đã tuân thủ kiến trúc.

## Cách kiểm chứng
Ở bước khởi tạo chỉ kiểm các đường dẫn và việc không phát sinh file. Khi có code: architecture test kiểm ProjectReference/type dependency, chiều phụ thuộc và ranh giới module; unit test invariant; demo trước/sau S,O,L,I,D trong lab Backend và giải thích một use case đi qua các tầng. Không báo architecture tests pass khi mới có thư mục.

## Nguồn
Buổi 2 tr.1–2; Buổi 3 tr.1–2; Buổi 4 tr.1–3 (cho phép dùng EF Core không thêm repository); Buổi 12 tr.2,4. Master III §7–8. Giải pháp kỹ thuật v2.1 §8.1–8.3, §9.
