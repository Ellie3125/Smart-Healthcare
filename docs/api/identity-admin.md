# Identity admin — schema gate

- contract_version: v0.1 Draft,18/11/2026. Contract identity v0.2 không đổi.
- Không tạo endpoint mới khi nhóm việc4 bị chặn schema.

| Web gọi | Trạng thái |
|---|---|
| GET `/api/identity/admin/permissions-matrix` | Policies.RoleOf/ChannelOf đã có; chưa tách triển khai khỏi việc4 đang bị chặn; không cần bảng permission |
| GET `/api/identity/admin/audit-logs?keyword=&page=` | shared.AuditLog có sẵn; chưa tách triển khai khỏi việc4; cần cursor/seek theo index và reader Shared giữ boundary |
| GET `/api/identity/admin/devices?date=` | Chờ device registry/assignment |
| PUT `/api/identity/admin/devices/{deviceId}/assignment` body `{accountUsername}` | Chờ schema gán thiết bị theo ngày/đúng tài khoản/phòng |

## Cần schema
ERD/model chỉ có `identity.RoomDeviceSession`: tài khoản phòng,DeviceId,IP,hash token,thời điểm login/logout,IsActive. Đây là dữ liệu bảo vệ phiên đăng nhập,không phải danh mục thiết bị bệnh viện hay gán thiết bị theo ngày. Không dùng endpoint admin assignment để tạo/chỉnh phiên hoặc token.

Cần owner thiết kế thiết bị(code,name,status) và assignment(device,account,date,room/station,actor/time,version/unique theo quy tắc nghiệp vụ). Quyết định loại tài khoản được gán và deviceId string/Guid phải chốt trước. Không tự tạo migration hoặc dùng dữ liệu Cloudflare/session làm bảng thay thế.

## Phần chỉ đọc có nguồn hiện tại
Matrix phải suy từ Policies.RoleOf/ChannelOf và policy thực tế,không duy trì một ma trận quyền độc lập trong DB. Admin chỉ AdminOnly,không mở quyền nghiệp vụ của doctor/nurse/reception/room.
Audit thuộc Shared,sử dụng reader Shared khi triển khai. Index hiện có theo actor/room/session/operation/shift và PK Id; không có timestamp+Id index. Không giả seek theo timestamp trên index chưa có; có thể chốt cursor PK thay page khi tách công việc. Không trả hash token hay thông tin định danh chưa che; giữ masking đang có.

Cả ba API chỉ đọc dự kiến AdminOnly/Staff. Không có runtime mới để test401/403/hasMore; chưa tuyên bố hoàn thành chức năng. Không thay đăng nhập/token/xác minh/Bootstrapper/frontend;không có yêu cầu sửa lõi trong PR tài liệu.
