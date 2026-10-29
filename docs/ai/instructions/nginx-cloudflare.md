# nginx-cloudflare

## Phạm vi
Codex: Nginx, domain/TLS, Cloudflare DNS/proxy và hosting server. Claude Code: cấu hình Cloudflare Access/Tunnel dạng file trong infrastructure/cloudflare/** và tài liệu docs/security/**. UI artifact do Antigravity tạo.

## Khi nào đọc
Định tuyến, upload path, CORS, TLS, rate limit, Access/Tunnel hoặc thiết kế hosting.

## Quy tắc
- **NET-01** — Client→Cloudflare (Public Edge cho bệnh nhân, Access cho Staff)→Tunnel→Nginx. Nginx tách host Public và Staff, chặn tuyến nội bộ qua host Public, phục vụ Frontend React/Vite build và proxy /api cùng origin tới BE1 và BE2 cùng image. Giữ SPA fallback trong route Frontend, không chuyển lỗi API thành index.html.
- **NET-02** — Chốt bảo toàn hay bỏ prefix /api theo contract; dấu / trong proxy_pass có ý nghĩa. nginx -t trước reload; upstream tới service name Docker. Demo hai instance thực sự nhận request; Codex dùng build UI được bàn giao, không sửa frontend/**.
- **NET-03** — Lab hosts/self-signed để minh họa trust; khi dùng Cloudflare HTTPS proxy: Full (Strict) và chứng chỉ hợp lệ đúng hostname cho đoạn tới origin. Không bypass TLS; chỉ tin forwarded headers từ proxy/Tunnel được chốt.
- **NET-04** — Body size/timeout/CORS/security headers theo endpoint và gói Cloudflare thực tế. Không cache chung bệnh án/API cá nhân ở Public Edge, không public SQL Server/chatbot/Ollama/Backend. Không áp browser challenge cho payment IPN.
- **NET-05** — Jenkins UI và công cụ quản trị đi qua mạng quản trị/VPN nếu chọn hoặc Access đã thiết kế; không public. VPN không phải điều kiện bệnh nhân vào web/app công khai.
- **NET-06** — Mọi web, API, SignalR, đường tệp nội bộ, thiết bị check in và chức năng nhân viên/bác sĩ/quản trị đi qua host Staff được Cloudflare Access bảo vệ. Policy Allow dùng Include cho email/nhóm SSO nhân viên hợp lệ và Require cho public IP/CIDR egress bệnh viện (danh tính VÀ mạng viện); không đặt email và IP thành hai Include. Access không thay kiểm quyền hồ sơ, vai trò và RoomId tại .NET.
- **NET-07** — Cloudflare Tunnel là đường vào duy nhất tới ứng dụng trong VPS; origin không mở cổng ứng dụng ra Internet và firewall chặn truy cập trực tiếp. Cấu hình Access/Tunnel lưu dạng file trong infrastructure/cloudflare, không chứa token, credential tunnel hay danh sách email thật; giá trị bí mật nạp từ secret runtime.

## Điều cấm
Không tự đổi hosting, thêm cloud storage ngoài R2 hoặc mở service nội bộ. Không gọi một host là HA hay đủ yêu cầu nhiều máy. Không coi presigned URL R2 là bằng chứng người tải ở mạng viện.

## Cách kiểm chứng
nginx -t; SPA refresh; API prefix; 413 trước/sau bằng dữ liệu giả; TLS chain; CORS; hai instance ID. Host Staff: đúng danh tính nhưng IP ngoài mạng viện bị Access chặn; đúng IP nhưng sai danh tính bị chặn; host Public không tới được tuyến Staff. Quét cổng origin từ ngoài có ủy quyền không thấy cổng ứng dụng. Kiểm Jenkins/công cụ quản trị không public ngoài policy.

## Nguồn
Phạm vi nghiệp vụ v2.1 §2.3; Giải pháp kỹ thuật v2.1 §8.6 [T11][T12]; Prompt_Stack_Smart_Healthcare §2,4,7–9; Buổi 8 tr.1–5. Cơ chế Cloudflare phải tra tài liệu phiên bản thực tế khi triển khai.
