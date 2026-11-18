# DocumentsReporting — báo cáo tổng hợp

- contract_version: v0.1 Draft,18/11/2026. Đây là API tổng hợp chỉ đọc trong DocumentsReporting;không tạo module analytics.
- `GET /api/documents/admin/reports/summary?dateFrom=yyyy-MM-dd&dateTo=yyyy-MM-dd`.
- Kênh Staff,policy AdminOnly. 401 ẩn danh;403 sai role hoặc Admin ở Patient. Không trả tên bệnh nhân,hồ sơ,token,chi tiết giao dịch hoặc dữ liệu định danh.

## Khoảng ngày và lỗi
Hai query bắt buộc,định dạng ISO date chính xác. Tính cả hai đầu,1..31 ngày theo GMT+7;from≤to. Không nhận ngày mặc định0001-01-01 hoặc to9999-12-31 để bảo đảm tính biên UTC không tràn. Sai/thiếu/ngược/quá dài trả400 ProblemDetails code `InvalidInput`,không truy vấn dữ liệu. `from` chuyển thành00:00 GMT+7;`until` là00:00 ngày sau `to`,lọc UTC `[from,until)`. GET không cần Idempotency-Key. Không pagination/totalCount;dailySeries luôn đủ ngày tăng dần,ngày trống điền0.

## Định nghĩa chỉ tiêu
| Trường | Nguồn và ý nghĩa |
|---|---|
| dateFrom,dateTo | yyyy-MM-dd,theo query đã xác thực |
| totalAppointments | Số Appointment có ConfirmedAtUtc trong khoảng;gồm lịch sau đó đã hủy/hoàn thành,không là số suất còn hoạt động |
| cancelledCount | Số Appointment có CancelledAtUtc trong khoảng;kể cả lịch được xác nhận trước khoảng |
| checkedInCount,totalEncounters | Số Encounter theo CheckedInAtUtc;hai tên cùng một chỉ tiêu,tính cả lượt sau đó Completed/Abandoned |
| totalTickets | Số QueueTicket theo IssuedAtUtc,tất cả loại/trạng thái;không đếm lại vé quay lại khi ActivatedAtUtc thay đổi |
| cashRevenue | Tổng PaymentTransaction.TransactionAmount có PaymentStatus=Success,Currency=VND,VerifiedAtUtc trong khoảng và PaymentIntent.Provider=Counter |
| onlineRevenue | Như trên với Provider=VNPAY;không gọi gateway/IPN/querydr |
| totalRevenue | cashRevenue+onlineRevenue,là tổng thực thu thành công trước hoàn;không phải doanh thu đã phân bổ vào từng khoản |
| totalRefunded | Tổng RefundTransaction.RefundedAmount có Status=Success,Currency=VND,ProcessedAtUtc trong khoảng;hoàn tiền hiện tại là tiền mặt tại quầy |
| currency | VND;tiền là JSON number/decimal,không đổi đơn vị hoặc làm tròn |

Không dùng tổng BillingItem hoặc join PaymentAllocation để tránh nhân đôi một giao dịch phân bổ vào nhiều khoản. Không cộng Pending/Failed/Unknown,Success chưa có VerifiedAtUtc hoặc tiền khácVND. Refund độc lập ngày thu,không trừ ngầm khỏi totalRevenue;thực thu ròng có thể tính totalRevenue-totalRefunded và có thể âm nếu hoàn khoản ngày trước.

`dailySeries`:mảng `{date,bookings,checkedIn,revenue,tickets,cashRevenue,onlineRevenue,refunded}`. bookings theo ngày xác nhận lịch,checkedIn theo ngày Encounter check-in,revenue là thực thu gộp theo VerifiedAt;không nhóm các chỉ tiêu theo ngày phục vụ tương lai. Tổng dailySeries tương ứng bằng các trường tổng;cancelledCount chỉ ở cấp tổng.

Ví dụ dữ liệu thử nghiệm:
```json
{"dateFrom":"2084-01-02","dateTo":"2084-01-02","totalAppointments":1,"checkedInCount":1,"cancelledCount":1,"totalRevenue":300,"totalRefunded":10,"dailySeries":[{"date":"2084-01-02","bookings":1,"checkedIn":1,"revenue":300,"tickets":1,"cashRevenue":100,"onlineRevenue":200,"refunded":10}],"totalEncounters":1,"totalTickets":1,"cashRevenue":100,"onlineRevenue":200,"currency":"VND"}
```

## Boundary và nhất quán
DocumentsReporting gọi `ISchedulingModule.GetDailyReportAsync`, `IClinicalModule.GetDailyReportAsync`, `IBillingModule.GetDailyReportAsync` với UTC interval `[from,until)` đã xác thực≤31ngày. Mỗi interface thêm method có default throw NotSupportedException để không phá fake hiện có. Bản thật dùng reader trong Infrastructure của module,mỗi query group ngày GMT+7 tại SQL,chỉ trả DTO BCL,không entity/IQueryable/connection. Scheduling query3nhóm(xác nhận/hủy/cấp vé),Clinical1nhóm(check-in),Billing2nhóm(thu/hoàn),không N+1 hoặc query chéo schema.

Billing chỉ đọc join PaymentIntent.Provider để phân loại nguồn thu;không sửa entity/trạng thái/luồng VNPAY,thu hoặc hoàn. Đây là số liệu đọc sống,không snapshot transaction xuyên module;dữ liệu có thể thay đổi giữa các query khi đang có giao dịch. API không tạo bản ghi chốt ca hay báo cáo kế toán bền. Schema hiện có chưa có index thời gian riêng cho mọi aggregate;không tự thêm migration,index mới là việc owner đánh giá nếu dữ liệu tăng.

## Ánh xạ cho frontend
| Web đang gọi | Backend thật / khác biệt |
|---|---|
| GET `/api/analytics/admin/reports/summary?dateFrom=&dateTo=` | Đổi sang `/api/documents/admin/reports/summary?dateFrom=&dateTo=`;giữ đủ field ReportSnapshotDto,thêm số lượt/vé và tách tiền mặt/online/refund;date ISO,tối đa31ngày |

Program.cs hiện đã gọi AddDocumentsReportingApplication/MapDocumentsReportingEndpoints;không cần sửa Bootstrapper. Không tạo alias analytics. Không cần schema/migration. Test SQL+HTTP kiểm timezone boundaries,status/tiền/totals,empty/31ngày và401/403;UI/E2E chưa kiểm. PR độc lập từ dev;các constructor/DI Scheduling/Clinical có điểm chung với PR#155,cần resolve/retest khi gộp các PR,không tự merge.
