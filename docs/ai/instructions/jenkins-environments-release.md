# jenkins-environments-release

## Phạm vi
Codex CI/CD, release, secrets và rollback.

## Khi nào đọc
Thiết kế pipeline hoặc thay deployment/backup.

## Quy tắc
- **OPS-02** — Jenkins là pipeline nghiệm thu chính; private repo→credential read-only→test→build image→push registry→webhook máy CD→pull→migration→deploy→smoke. Test fail phải chặn các bước sau.
- **OPS-03** — Rollback dùng image đã test theo digest/tag bất biến và release manifest; không rebuild. Kiểm schema compatibility trước rollback; không tự đảo migration phá dữ liệu.
- **REL-01** — Buổi 10 cần máy CI/CD tách biệt; Buổi 11 Dev/Staging/Prod trên nhiều máy. Jenkins có thể ở Dev. VM là phương án cần thống nhất học phần; ba Compose project trên một host chưa chứng minh đủ nhiều máy.
- **REL-02** — Multibranch dev/staging/main ánh xạ allowlist môi trường; feature branch không được fallback tự deploy Dev. Database, secrets, JWT issuer/audience/key, volumes và storage tách riêng.
- **REL-03** — CD chỉ nhận release reference đã xác minh, không shell tùy ý; webhook auth, kiểm replay/timestamp, allowlist registry/digest/environment, deploy lock, log redacted. Máy CD không checkout/build source.
- **REL-04** — Pin nguồn dependency/image/plugin, SSH host key phải xác minh. Credentials tách checkout/push/deploy; secret không xuất trong console/image. Không mặc định privileged/root/socket từ ví dụ Jenkins.
- **REL-05** — Ghi release gồm code commit, image digest, model/schema version, môi trường, migration, smoke và previous release. Readiness fail phải có trạng thái thất bại rõ; monitor API, tài nguyên, backup age, cảnh báo không chứa dữ liệu bệnh nhân.
- **REL-06** — Backup DB + file + cấu hình/khóa cần thiết, mã hóa ngoài VPS; restore test độc lập và đo RPO/RTO. Kuma cùng VPS không tự cảnh báo lúc VPS chết, cần probe ngoài. Actions chỉ PR check, không cùng deploy một release.

## Điều cấm
Không dùng latest để rollback, không tắt host verification, không chạy các script Jenkins/registry đính kèm nguyên trạng. Không hứa zero downtime/HA từ một VPS.

## Cách kiểm chứng
Demo test fail dừng pipeline; sửa endpoint→commit→CI→API thay đổi trên máy CD; JWT Dev bị Staging 401; dừng consumer/DB thử có kiểm soát; rollback không build; restore và reboot có dữ liệu.

## Nguồn
Buổi 10 tr.1–6; Buổi 11 tr.1–5; PDF code Jenkins tr.1 dẫn repo đã rà soát. Master H5–6/I §12,14.
