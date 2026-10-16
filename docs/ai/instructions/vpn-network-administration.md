# vpn-network-administration

## Phạm vi
Codex, chỉ khi chủ dự án chọn triển khai VPN. Đây là hướng dẫn điều kiện, không là quyết định bật VPN.

## Khi nào đọc
Khi thiết kế mạng quản trị từ xa, CI/CD liên máy hoặc site-to-site. Các việc độc lập vẫn tiếp tục khi chưa có thông tin VPN.

## Quy tắc
- **VPN-01** — VPN là mạng riêng ảo, khác VPS. WireGuard chỉ là ứng viên cần khảo sát, chưa phải công nghệ đã chốt. Xác định mục đích remote administration, CI/CD connectivity hoặc site-to-site trước lựa chọn.
- **VPN-02** — Trước cấu hình thực tế cần gateway/host, IP public/NAT, client OS, mạng/subnet và tài nguyên được phép. Thiết kế peer/key riêng, routes/split tunnel nếu phù hợp, firewall/ACL, thu hồi peer và hạn chế quyền. Không mở rộng quyền mạng mặc định cho toàn LAN.
- **VPN-03** — Kiểm đường đi VPN riêng theo giao thức được chọn; không giả Cloudflare web proxy chuyển tiếp UDP/TCP VPN tùy ý. VPN không thay TLS, ứng dụng auth hoặc object-level authorization; bệnh nhân vẫn vào website công khai không cần VPN.
- **VPN-04** — Chuẩn bị đường truy cập dự phòng và rollback config trước đổi routing/firewall. Giữ private key ngoài prompt/repo/image/log; backup config an toàn. Chỉ chạy cấu hình trên hệ thống được ủy quyền khi mục tiêu mạng đã chốt.

## Điều cấm
Không tự cài VPN hoặc coi VPN là VPS; không public SQL Server/ML/MinIO Console/Jenkins để tránh xử lý kết nối quản trị. Không yêu cầu người dùng gửi private key vào chat.

## Cách kiểm chứng
Khi được chọn: peer hợp lệ vào đúng subnet/service; peer bị thu hồi bị từ chối; ngoài tunnel không vào admin services; public website hoạt động bình thường; reconnect/reboot/rollback mạng không khóa quản trị. Ghi bài test và log redacted thật, hiện tại Not run.

## Nguồn
Prompt_Stack_Smart_Healthcare §4,9–10. Tài liệu chính thức VPN sẽ được tra cứu khi có quyết định công nghệ; chưa có sản phẩm VPN đã pin.
