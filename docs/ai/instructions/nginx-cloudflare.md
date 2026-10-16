# nginx-cloudflare

## Phạm vi
Codex: reverse proxy, domain/TLS, Cloudflare và hosting server; UI artifact do Antigravity tạo.

## Khi nào đọc
Định tuyến, upload path, CORS, TLS, rate limit hoặc thiết kế hosting.

## Quy tắc
- **NET-01** — Browser→Cloudflare→Nginx; Nginx phục vụ Frontend React/Vite build và proxy /api cùng origin tới hai Backend cùng image. Giữ SPA fallback trong route Frontend, không chuyển lỗi API thành index.html.
- **NET-02** — Chốt bảo toàn hay bỏ prefix /api theo contract; dấu / trong proxy_pass có ý nghĩa. nginx -t trước reload; upstream tới service name Docker. Demo hai instance thực sự nhận request; Codex dùng build UI được bàn giao, không sửa frontend/**.
- **NET-03** — Lab hosts/self-signed để minh họa trust; khi dùng Cloudflare HTTPS proxy: Full (Strict), origin certificate hợp lệ đúng hostname, origin firewall. Không bypass TLS; chỉ tin forwarded headers từ proxy được chốt.
- **NET-04** — Body size/timeout/CORS/security headers theo endpoint và gói Cloudflare thực tế. Không cache bệnh án/API cá nhân, không public SQL Server/ML/Backend. Không áp browser challenge cho payment IPN.
- **NET-05** — MinIO Console và Jenkins UI qua mạng quản trị/VPN nếu chọn hoặc access hạn chế đã thiết kế. MinIO S3 API cho browser, nếu chọn presigned direct upload, là endpoint dữ liệu riêng có TLS/CORS/authorization; không nhầm với console. Xem object-storage.md. VPN không phải điều kiện bệnh nhân vào web công khai.

## Điều cấm
Không tự đổi hosting, triển khai thêm cloud storage hoặc mở service nội bộ. Không gọi một host là HA hay đủ yêu cầu nhiều máy.

## Cách kiểm chứng
nginx -t; SPA refresh; API prefix; 413 trước/sau bằng dữ liệu giả; TLS chain; CORS; hai instance ID; external port check có ủy quyền. Khi dùng presigned URL, browser phải resolve/reach đúng endpoint; test TLS và upload/download thật. Kiểm console/Jenkins không public ngoài policy.

## Nguồn
Prompt_Stack_Smart_Healthcare §2,4,7–9; Buổi 8 tr.1–5. Cơ chế Cloudflare/MinIO phải tra phiên bản thực tế khi triển khai.
