# Billing — API quản trị

- contract_version: v0.1 Draft,18/11/2026,đợt2 A1. Không thay contract/luồng VNPAY,thu hoặc hoàn tiền.

## Bảng giá
`GET /api/billing/admin/price-lists?page=1&pageSize=50`, `POST /api/billing/admin/price-lists`. Cả hai Staff/AdminOnly:401ẩn danh,403sai vai trò/kênh hoặc actor POST không có AppUserId hợp lệ. Không chấp nhận admin gọi API vai trò khác.

GET:page1..1000,size1..100,response `{items,page,pageSize,hasMore}`,đọc size+1;sort EffectiveFromUtc giảm dần,Id tăng. Mỗi item `{id,code,name,effectiveFrom,effectiveTo?,isActive,consultationPrices:[{rankName,price}]}`. Ngày yyyy-MM-dd theo GMT+7;không totalCount. Mỗi trang dùng batch tra cấp bậc qua Identity;rank không còn trong danh mục active được hiển thị bằng ID,không đoán tên. Chỉ consultationPrices theo cấp bậc,không gộp doctor override/CLS vào phí khám theo rank.

POST body giống item bỏ id,không nhận giá từ bảng khác:code không trống≤64,name không trống≤200;effectiveFrom bắt buộc ISO date không là0001-01-01,effectiveTo nullable,phải lớn hơnfrom. Tính hiệu lực `[00:00 from GMT+7,00:00 to GMT+7)`,nullto không hết hạn. isActive là bool. consultationPrices1..50dòng,rankName khớp đúng một cấp bậc active qua IIdentityPatientsModule(không phân biệt hoa/thường,bỏ khoảng trắng đầu/cuối),không trùngrank;price≥0,decimal(18,2),không làm tròn âm thầm. Ví dụ dữ liệu thử:
```json
{"code":"DEMO-2085","name":"Giá thử nghiệm","effectiveFrom":"2085-01-01","effectiveTo":"2085-01-15","isActive":true,"consultationPrices":[{"rankName":"Bác sĩ","price":150000}]}
```
Response201 là item cóid. Đây là tạo mới,không có update/activate/đóng hiệu lực giá cũ trong đợt này. Chỉ quản trị phí khám theo body hiện có của web;không mở thêm cách ghi giáCLS/giá riêng bác sĩ chưa có contract body.

**Chọn409khi chồng**,không tự đóng bản cũ:hai list active,VND,cùng consultation rank(bỏ doctor-specific override) có khoảng hiệu lực giao nhau thì409PriceOverlap. Khoảng sát biên được tạo,giá mới áp từđúngbiên;inactive list không tham gia resolver. POST serialize ngắn catalog PriceList trong UoW bằng khóa SQL trước check+insert,kể cả bảng trống;hai request cạnh tranh chỉ một được tạo. Trùngcode409CodeAlreadyExists(check+uniqueDB). Không sửa BillingItem/giá snapshot đã thu;IPriceCatalog/PriceResolver hiện có vẫn là nguồn báo giá.

Optional header Idempotency-Key:1..128ký tựASCII hiển thị,không khoảng trắng;TTL24h,theoAppUser+Billing.CreatePriceList. Vắng header vẫn tạo mới bằng guard DB. Cùngkey/nội dung chuẩn hóa(trim,date,rank casing,thứ tự giá,decimal2) replay201cùng kết quả,kể cả rank sau đó ngừng active;khácnội dung409IdempotencyConflict,bản ghi đang chạy409RequestInProgress. Khóa sai400. Business create+AuditLog(BillingAuditWriter)+kết quảidempotency cùng transaction;không externalIO.

Lỗi:400InvalidInput(JSONnull/sai,input,phân trang,rankkhôngduynhất/khôngactive),415UnsupportedMediaType,409PriceOverlap/CodeAlreadyExists/IdempotencyConflict/RequestInProgress. Mọi lỗi module là ProblemDetails cócode;401/403middleware có thể không body. JSONnumber choVND;không tự đổi đơn vị.

| Web gọi | Backend / khác biệt |
|---|---|
| GET `/api/billing/admin/price-lists` | Cùngroute;đọc envelope.items thay mảng trực tiếp,phân trang vàhasMore |
| POST `/api/billing/admin/price-lists` | Cùngroute/body hiện có;effectiveTo là ngày kết thúc exclusive;POST201;optionalIdempotency-Key,nếu lịch chồng nhận409 |

Không cần schema hoặc sửa lõi. Không tạo/sửa migration,frontend,VNPAY/PaymentIntent. Tiếp tục miễn phíCLS và thiết bị ở trạng thái bỏMVP,không là blocker bảng giá. UI/E2E chưa kiểm.
