# sqlserver-efcore

## Phạm vi
SQL Server, EF Core, migration, concurrency và recovery.

## Khi nào đọc
Khi xử lý dữ liệu, giữ chỗ, callback hoặc phát hành schema.

## Quy tắc
- **DB-01** — SQL Server là database nghiệp vụ duy nhất. Chốt edition/license, phiên bản, kiến trúc CPU/RAM/container và EF provider từ tài liệu Microsoft trước triển khai. Developer edition chỉ cho mục đích được license cho phép.
- **DB-02** — Giữ chỗ và idempotency phải nguyên tử xuyên hai backend: chọn atomic conditional update/transaction/constraint thích hợp. rowversion phát hiện xung đột cập nhật, không tự ngăn phantom hoặc mọi dạng overbooking.
- **DB-03** — Migration chạy một tác vụ có kiểm soát trong release, có lock deploy, identity riêng và review SQL. Không để hai replica tự migrate; ưu tiên expand/contract và migration tương thích rollback.
- **DB-04** — Idempotency record có unique key theo actor/operation/key, request hash, trạng thái và kết quả. Cùng key khác payload phải báo conflict; replay trả cùng kết quả; TTL phải được chốt. Không dùng cache RAM làm nguồn duy nhất.
- **DB-05** — Map GUID/uniqueidentifier, tiền decimal có precision/scale rõ, thời gian UTC với kiểu thống nhất; rowversion là token nhị phân, không phải ngày giờ. Index theo truy vấn thật; truy vấn có tham số, giới hạn page size, tránh N+1.
- **DB-06** — Retry có giới hạn, chạy lại toàn unit transaction khi phù hợp execution strategy; không lặp side effect thanh toán/email. Test trên SQL Server cùng dòng/edition phù hợp, không dùng EF InMemory/SQLite thay bằng chứng DB.
- **DB-07** — Backup SQL Server native có kiểm tra, mã hóa ngoài host, retention và khóa phục hồi. Restore vào môi trường tách biệt, kiểm liên kết dữ liệu và đọc file liên quan; VERIFYONLY hoặc file .bak tồn tại chưa chứng minh phục hồi.

## Điều cấm
Không tắt xác minh TLS để che lỗi; không public SQL Server; không đưa tài khoản sa vào runtime ứng dụng; không down -v để sửa lỗi kết nối.

## Cách kiểm chứng
Test capacity=1 với nhiều request qua hai instance; cùng idempotency key; request khác payload; deadlock/retry; race expire/pay; migrate DB cũ; restore backup và smoke test. Ghi SQL Server version và image digest trong evidence.

## Nguồn
Master III §2; Buổi 7 tr.4; Buổi 10 tr.4–6; Microsoft EF Core concurrency, transactions, applying migrations tại https://learn.microsoft.com/ef/core/.
