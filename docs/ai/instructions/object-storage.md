# object-storage

## Phạm vi
Codex: MinIO, metadata SQL Server, adapter và quyền file; Antigravity: upload/download UI theo contract.

## Khi nào đọc
Thiết kế quản lý file, presigned URL, đổi key, phát hành tài liệu hoặc backup/restore.

## Quy tắc
- **OBJ-01** — MinIO tự quản lý là object storage chính. Không tự thêm provider storage khác. SQL Server giữ metadata/owner/liên kết hồ sơ-chỉ định/object key/trạng thái phát hành; MinIO giữ bytes. Bucket y tế private; backend kiểm object-level authorization; key ngẫu nhiên không chứa tên/CCCD.
- **OBJ-02** — Chọn và ghi ADR một luồng file: (A) presigned browser trực tiếp tới endpoint MinIO truy cập được, TLS/CORS/TTL ngắn, không hostname Docker nội bộ; hoặc (B) Backend proxy/stream nếu storage không có endpoint browser, đánh giá memory/bandwidth/body/timeout và quyền. Intent→upload→server xác minh size/type/nội dung/checksum phù hợp→liên kết→kiểm/phát hành. Không tin Content-Type hoặc ghi đè file đã phát hành.
- **OBJ-03** — Move/rename là copy rồi delete có kiểm chứng, không atomic rename; xử lý lỗi giữa storage và DB, upload dở/orphan và reconcile metadata. Demo public link chỉ trên bucket/file mẫu không nhạy cảm. Không log URL ký/token đầy đủ.
- **OBJ-04** — Xác minh nguồn phân phối, license, phiên bản, SDK và vận hành MinIO trước pin; không thay công nghệ khi gặp giới hạn. Demo SDK upload/download/move/delete, expiry và quyền private. Backup/restore bao gồm objects, DB, metadata mapping và khóa/cấu hình cần thiết; volume không là backup, MinIO cùng host không là HA.

## Điều cấm
Không access/secret key trong frontend, repo hoặc image; không mở Console public để tránh cấu hình mạng quản trị. Không suy Cloudflare proxy có thể phục vụ mọi kích thước/timeout. Không tự bật dịch vụ storage khác; xác minh quota/chi phí dịch vụ thực dùng khi triển khai.

## Cách kiểm chứng
Test quyền A/B, expiry, browser endpoint, TLS/CORS, file lớn/sai magic bytes, upload dở/orphan, copy lỗi không delete nguồn, restore DB cùng objects đọc được tài liệu giả. Test checksum tải xuống và metadata đúng lượt khám. Ghi kiểu transport đã chọn và giới hạn đã đo.

## Nguồn
Prompt_Stack_Smart_Healthcare §2,4,6–7; Buổi 9 tr.1–4. Thiết kế bytes/metadata/quyền đã thống nhất theo MinIO.
