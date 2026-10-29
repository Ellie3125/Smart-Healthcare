---
name: cloudflare-access-tunnel
description: "Dùng khi viết hoặc review cấu hình Cloudflare Access policy và Tunnel dạng file trong infrastructure/cloudflare/, hoặc tài liệu biên mạng trong docs/security/."
---

# Cloudflare Access và Tunnel

1. Đọc nginx-cloudflare.md (NET-01, NET-06, NET-07), healthcare-domain.md (SEC-02) và tra tài liệu Cloudflare hiện hành cho Access policy và cloudflared.
2. Ghi cấu hình dạng file trong infrastructure/cloudflare/: ingress Tunnel tới Nginx, tách hostname Public và Staff, catch-all trả 404.
3. Policy Allow cho host Staff: Include email/nhóm SSO nhân viên, Require IP/CIDR egress bệnh viện. Không đặt email và IP thành hai Include.
4. Không ghi token, credential tunnel, email hay IP thật; dùng biến/placeholder và ghi nguồn secret runtime.
5. Thay đổi Nginx hoặc compose cần cho Tunnel thuộc Codex: dùng skill `handoff`.

## Checklist kiểm chứng
- [ ] Từ ngoài mạng bệnh viện, đúng danh tính: host Staff bị chặn.
- [ ] Từ mạng bệnh viện, sai danh tính: bị chặn.
- [ ] Host Public không tới được tuyến Staff.
- [ ] Quét cổng origin từ ngoài (có ủy quyền) không thấy cổng ứng dụng nào mở.
- [ ] Không có secret trong diff; kiểm chưa chạy thì ghi Not run trong evidence.
