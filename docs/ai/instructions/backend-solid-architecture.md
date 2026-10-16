# backend-solid-architecture

## Phạm vi
ASP.NET Core backend và tests kiến trúc.

## Khi nào đọc
Thiết kế module/use case, sửa dependency hoặc DI.

## Quy tắc
- **ARCH-01** — Backend dùng Modular Monolith ngay từ đầu, áp dụng Clean Architecture bên trong từng module. Toàn Backend nghiệp vụ là một ứng dụng triển khai thống nhất, có ít nhất ba module nghiệp vụ thực chất; mỗi module sở hữu dữ liệu/schema và public contract. Xem database-module-boundaries.md.
- **ARCH-02** — Mỗi module tách Domain, Application, Infrastructure, Presentation và Contracts. Chiều phụ thuộc hướng vào Domain/Application; Contracts không phụ thuộc implementation. Api là host/composition root lắp ghép module; không gom nghiệp vụ vào host. Không gọi chéo DbContext/Repository.
- **ARCH-03** — Áp dụng SOLID có lý do thay đổi: tách nghiệp vụ khỏi IO, interface theo nhu cầu client, thay implementation không phá contract. Không bắt buộc generic repository/base service/CQRS hay interface cho mọi class. Trước code chốt domain/ERD và UML cần thiết: use case, activity phân vai, sequence đặt lịch/ML/thanh toán/trả kết quả, class/domain, component, deployment và state machines; cập nhật theo implementation thật.
- **BE-01** — Controller xử lý transport/validation rồi gọi use case. Dùng DTO tách entity; nullable rõ; async IO và CancellationToken; DI lifetime đúng, không capture scoped DbContext trong singleton.
- **BE-02** — Giữ framework test/namespace của repo khi có; repo mới chốt bằng ADR. Không thêm Semantic Kernel/Azure/telemetry vì skill gợi ý. Pin SDK/EF provider tương thích; build reproducible.

## Cấu trúc Backend ban đầu

| Đường dẫn | Vai trò |
|---|---|
| backend/src/Api/ | Host ASP.NET Core, composition root, middleware và đăng ký module |
| backend/src/Modules/ | Các module theo ranh giới nghiệp vụ |
| backend/src/SharedKernel/ | Chỉ primitive/abstraction thực sự dùng chung, không shared entity/DbContext |

Các module dự kiến: Identity, Patients, Scheduling, Clinical, Billing, Documents, Notifications và Reporting. Đây là ranh giới ban đầu; chốt chi tiết dữ liệu/transaction/contract qua phân tích nghiệp vụ trước implementation.

Với mỗi module, dùng các thư mục:
- Domain/: entity, value object và invariant; không phụ thuộc EF Core, ASP.NET Core hoặc tầng ngoài.
- Application/: use case và port; phụ thuộc Domain và contract cần thiết, không phụ thuộc Infrastructure/Presentation.
- Infrastructure/: EF Core SQL Server, repository/adapter hiện thực port; phụ thuộc hướng vào Application/Domain.
- Presentation/: endpoint/controller của module, gọi Application; không thay thế Frontend React.
- Contracts/: DTO/event/public interface cho giao tiếp module; không expose EF entity, DbContext, repository, IQueryable hoặc kiểu nội bộ. Không tham chiếu implementation của module.

Không tạo một bộ Domain/Application/Infrastructure dùng chung cho toàn bộ nghiệp vụ. Module khác chỉ dùng contract được công bố, không truy cập trực tiếp schema/module data. Chỉ chia sẻ phần SharedKernel tối thiểu có lý do.

Hai Backend replica chạy cùng image của một Modular Monolith. FastAPI ML là service nội bộ riêng; không tách mỗi module nghiệp vụ thành container/microservice. Chỉ tách module thành service sau này khi có nhu cầu rõ về vận hành, scale hoặc ranh giới đội ngũ và có thiết kế được chấp thuận.

Nếu nhiệm vụ hiện tại chỉ khởi tạo cây thư mục, tạo đúng thư mục rỗng; không tạo solution/project, class/interface, DbContext, migration, file mẫu hoặc .gitkeep. Việc có thư mục chưa chứng minh module boundary hoặc dependency đã được kiểm chứng.

## Điều cấm
Không expose EF entity trực tiếp làm API, không sync-over-async, không hard-code quyền trong UI. Không sinh abstraction, base class hoặc tầng trung gian không có trách nhiệm thực chất. Không biến cấu trúc thư mục thành bằng chứng code đã tuân thủ kiến trúc.

## Cách kiểm chứng
Ở bước khởi tạo chỉ kiểm các đường dẫn và việc không phát sinh file. Khi có code: architecture test kiểm ProjectReference/type dependency, chiều phụ thuộc và ranh giới module; unit test invariant; demo trước/sau S,O,L,I,D trong lab Backend và giải thích một use case đi qua các tầng. Không báo architecture tests pass khi mới có thư mục.

## Nguồn
Buổi 2 tr.1–2; Buổi 3 tr.1–2; Buổi 4 tr.1–3 (cho phép dùng EF Core không thêm repository); Buổi 12 tr.2,4. Master III §7–8.
