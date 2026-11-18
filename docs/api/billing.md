# Billing — API quản trị

Contract v0.2 Draft,18/11/2026. Bổ sung chuyển kỳ bảng giá mở theo review. Không thay VNPAY hoặc cách ghi phiếu thu/hoàn.

## Bảng giá

GET `/api/billing/admin/price-lists?page=1&pageSize=50`, POST `/api/billing/admin/price-lists`. Cả hai dùng AdminOnly/Staff:401 ẩn danh,403 sai role/kênh; actor POST phải có AppUserId hợp lệ.

GET: page1..1000, pageSize1..100; response `{items,page,pageSize,hasMore}`, lấy size+1; sort EffectiveFromUtc giảm dần,Id tăng. Item `{id,code,name,effectiveFrom,effectiveTo?,isActive,consultationPrices:[{rankName,price}]}`; ngày yyyy-MM-dd GMT+7, không totalCount. Batch directory cấp bậc qua Identity; rank đã mất khỏi danh mục active hiển thị ID. consultationPrices chỉ gồm phí khám theo cấp bậc, không gộp giá riêng bác sĩ/CLS.

POST body cùng shape trừ id. Code trim1..64, name trim1..200, ngày ISO hợp lệ từ0001-01-02, effectiveTo null hoặc lớn hơn effectiveFrom. Bắt buộc1..50 consultationPrices, rankName trim và khớp duy nhất danh mục active, không trùng cấp bậc; price decimal18,2 từ0 đến9999999999999999.99, không làm tròn. Giá có hiệu lực từ đầu ngày GMT+7, effectiveTo là đầu ngày kết thúc exclusive. Response201 là bảng giá mới.

## Chuyển kỳ khi bảng cũ chưa có ngày kết thúc

Trong cùng UoW Billing+Shared, khóa catalog PriceList bằng TABLOCKX/HOLDLOCK trước kiểm tra và ghi:

- Bảng mới inactive không đóng hoặc xung đột với các bảng đang áp dụng.
- Với bảng mới active, tìm các kỳ active VND có item ConsultationFee active, DoctorId null và cấp bậc được thay, giao khoảng mới.
- Nếu bất kỳ kỳ giao nào có EffectiveToUtc hoặc bắt đầu từ/sau mốc mới:409 PriceOverlap, không đóng kỳ nào. Khoảng finite không giao và hai khoảng kề nhau vẫn hợp lệ.
- Các bảng mở giao khoảng mới và bắt đầu trước mốc mới được đặt EffectiveToUtc bằng EffectiveFromUtc của bảng mới. Bản cũ giữ IsActive/item/giá/ID để tra lịch sử; không sửa BillingItem hoặc snapshot khoản đã thu.
- Một bảng seed có thể chứa cả cấp bậc khác, giá riêng bác sĩ và CLS. Các item active không được thay được sao chép sang một PriceList tiếp nối active không có ngày kết thúc, từ mốc mới, mã tự sinh CONT-<guid>, tên Tiếp nối <mã cũ>. Không tạo kỳ tiếp nối nếu không có item cần giữ. Cách này giữ các giá không đổi cả khi bảng mới có ngày kết thúc. Không tự khôi phục giá cũ của cấp bậc đã thay sau khi kỳ mới hết hạn; admin tạo kỳ tiếp theo nếu cần.
- Audit UPDATE bảng cũ có before/after EffectiveToUtc; audit INSERT bảng tiếp nối ghi SourceId và số item; audit INSERT bảng mới. Tất cả dùng cùng thời điểm IDbClock và cùng transaction với idempotency. Audit lỗi rollback cả đóng kỳ, sao chép và tạo mới. Không external IO.

Ví dụ seed không có ngày kết thúc: giá100 từ01/01/2026, admin tạo giá200 từngày mai →201; giá100 áp trước mốc, giá200 áp từ mốc; không AmbiguousPrice. Giá CLS/cấp bậc không đổi vẫn áp dụng, snapshot đã thu100 giữ nguyên.

Trùng code409 CodeAlreadyExists (kiểm tra + uniqueDB). Khóa catalog ngăn hai request cùng mốc tạo hai kỳ chồng; chỉ một thành công. IPriceCatalog/PriceResolver vẫn là nguồn báo giá; không migration mới.

## Idempotency và lỗi

Optional một Idempotency-Key,1..128 ký tự ASCII hiển thị, không khoảng trắng; TTL24h, theo AppUser+Billing.CreatePriceList. Cùng key và nội dung chuẩn hóa (trim/date/rank casing/order/decimal2) replay201 cùng kết quả, kể cả rank sau đó inactive; không lặp đóng kỳ/continuation/audit. Khác payload409 IdempotencyConflict; đang chạy409 RequestInProgress. Không key vẫn có guard DB.

400 InvalidInput khi input/JSON/rank/phân trang sai;415 UnsupportedMediaType;409 PriceOverlap/CodeAlreadyExists/IdempotencyConflict/RequestInProgress. ProblemDetails có code;401/403 middleware có thể không body. Tiền JSON number/VND, không đổi đơn vị.

## Ánh xạ cho frontend

| Web gọi | Backend / khác biệt |
|---|---|
| GET /api/billing/admin/price-lists | Cùng route; đọc envelope.items, pagination/hasMore; có thể thấy bảng CONT tự sinh giữ các giá chưa thay |
| POST /api/billing/admin/price-lists | Cùng body/route;201, effectiveTo exclusive; tự đóng bảng mở trước mốc, finite overlap/bắt đầu≤kỳ cũ trả409; optional Idempotency-Key |

Không schema/core/frontend change. Miễn CLS và thiết bị vẫn bỏMVP; UI/E2E/Staging thực tế chưa kiểm.
