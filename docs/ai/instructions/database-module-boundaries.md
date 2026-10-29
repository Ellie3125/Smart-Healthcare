# database-module-boundaries

## Phạm vi
Quyền sở hữu schema và giao tiếp module.

## Khi nào đọc
Thiết kế ERD, query/report, event hoặc thêm DbContext.

## Quy tắc
- **MOD-01** — Mỗi module có schema và DbContext riêng trong SQL Server; owner duy nhất. Module khác không query bảng, JOIN hoặc dùng repository/DbContext của owner.
- **MOD-02** — Đọc xuyên module qua application contract, DTO hoặc projection do subscriber sở hữu. Không trả IQueryable, EF entity hoặc connection từ module ra ngoài.
- **MOD-03** — Transaction giữ slot + appointment nằm trong module Scheduling; không chia hai bước kiểm rồi giữ qua network. Cross-module workflow ghi rõ consistency/compensation/outbox; không giả transaction phân tán.
- **MOD-04** — Shared kernel tối thiểu: ID, clock abstraction, contract ổn định nếu cần. Không tạo shared DbContext hoặc kho entity dùng chung. Migration của từng schema có chủ sở hữu.
- **MOD-05** — Module chỉ giao tiếp bằng bốn cách: (1) gọi đồng bộ qua `<Module>.Contracts`; (2) lệnh nguyên tử nhiều module do application service của module khởi phát điều phối qua IUnitOfWork, các DbContext dùng chung DbConnection/DbTransaction; (3) integration event qua Outbox và worker Hangfire, handler chống lặp bằng EventId; (4) domain event trong module trước commit. Chọn theo thứ tự: cần cùng commit/rollback → (2); cần kết quả ngay → (1); còn lại → (3). Không gọi cổng thanh toán, model, R2 hay push trong transaction của (1)/(2). Truy vấn tổng hợp ghép DTO từ contract; Reporting đọc view chỉ đọc do module công bố.

## Điều cấm
Không dùng stored procedure/view/query raw để lách boundary. Không tạo FK chéo schema; tham chiếu chéo chỉ bằng ID (PatientId, EncounterId, DoctorId) và contract xác minh. Không để hai module phụ thuộc vòng lúc biên dịch.

## Cách kiểm chứng
Kiểm dependency/type rules và query access; review ERD ghi owner; test contract ở biên; demo tách module chỉ thay adapter/network, không truy cập dữ liệu module khác.

## Nguồn
Giải pháp kỹ thuật v2.2 §8.1.5, §8.2; Buổi 12 tr.2–5; Master III SQL Server; Thiết kế module/schema phải được chốt trước implementation; không giả một bản ADR mẫu là quyết định đã duyệt.
