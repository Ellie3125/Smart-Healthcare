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

## Điều cấm
Không dùng stored procedure/view/query raw để lách boundary. FK xuyên schema nếu cần phải có ADR đánh giá coupling; mặc định dùng ID tham chiếu và contract xác minh.

## Cách kiểm chứng
Kiểm dependency/type rules và query access; review ERD ghi owner; test contract ở biên; demo tách module chỉ thay adapter/network, không truy cập dữ liệu module khác.

## Nguồn
Buổi 12 tr.2–5; Master III SQL Server; Thiết kế module/schema phải được chốt trước implementation; không giả một bản ADR mẫu là quyết định đã duyệt.
