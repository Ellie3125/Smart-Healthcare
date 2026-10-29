# jenkins-environments-release

## Phạm vi
Codex CI/CD, release, secrets và rollback.

## Khi nào đọc
Thiết kế pipeline hoặc thay deployment/backup.

## Quy tắc
- **OPS-02** — Jenkins là pipeline nghiệm thu chính; private repo→credential read-only→test→build image một lần trên CI→push registry→webhook máy CD→pull đúng digest (Staging rồi Prod)→migration→deploy→smoke. VPS không build source. Test fail phải chặn các bước sau.
- **OPS-03** — Rollback dùng image đã test theo digest/tag bất biến và release manifest; không rebuild. Kiểm schema compatibility trước rollback; không tự đảo migration phá dữ liệu.
- **REL-01** — Dev chạy trên laptop, Staging trên VPS 1 riêng, Prod trên VPS 2 riêng; không dùng chung một máy cho Staging và Prod. Jenkins (CI) chạy trên laptop; ba Compose project trên một host chưa chứng minh đủ nhiều máy. Rủi ro đã chấp nhận: laptop tắt thì không có CI; Jenkins trên laptop không giữ secret Prod ngoài credential store.
- **REL-02** — Multibranch dev/staging/main ánh xạ allowlist môi trường; feature branch không được fallback tự deploy Dev. Mỗi môi trường dùng compose.yaml cùng override và .env riêng; database, secrets, JWT issuer/audience/key, volumes, bucket R2 và Access policy tách riêng. Staging là cổng nghiệm thu trước Prod: cùng image digest, OS/kiến trúc CPU, Cloudflare Access/Tunnel, R2 private, cổng thanh toán sandbox và profile `chatbot` (VPS Staging đủ RAM cho Ollama); chỉ khác secrets, dữ liệu và hostname.
- **REL-03** — CD chỉ nhận release reference đã xác minh, không shell tùy ý; webhook auth, kiểm replay/timestamp, allowlist registry/digest/environment, deploy lock, log redacted. Máy CD không checkout/build source.
- **REL-04** — Pin nguồn dependency/image/plugin, SSH host key phải xác minh. Credentials tách checkout/push/deploy; secret không xuất trong console/image. Không mặc định privileged/root/socket từ ví dụ Jenkins.
- **REL-05** — Ghi release gồm code commit, image digest, model/schema version, môi trường, migration, smoke và previous release. Readiness fail phải có trạng thái thất bại rõ; monitor API, tài nguyên, backup age, cảnh báo không chứa dữ liệu bệnh nhân.
- **REL-06** — Backup DB + file + cấu hình/khóa cần thiết, mã hóa ngoài VPS; restore test độc lập và đo RPO/RTO. Kuma cùng VPS không tự cảnh báo lúc VPS chết, cần probe ngoài. Actions chỉ PR check, không cùng deploy một release.

## Điều cấm
Không dùng latest để rollback, không tắt host verification, không chạy các script Jenkins/registry đính kèm nguyên trạng. Không hứa zero downtime/HA từ một VPS.

## Cách kiểm chứng
Demo test fail dừng pipeline; sửa endpoint→commit→CI→API thay đổi trên máy CD; JWT Dev bị Staging 401; dừng worker/DB thử có kiểm soát; rollback không build; restore và reboot có dữ liệu.

## Nguồn
Buổi 10 tr.1–6; Buổi 11 tr.1–5; PDF code Jenkins tr.1 dẫn repo đã rà soát. Master H5–6/I §12,14.
