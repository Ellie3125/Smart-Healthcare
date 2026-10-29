# object-storage

## Phạm vi
Codex: Cloudflare R2 private, metadata SQL Server, adapter và quyền tệp ở Backend/worker. Antigravity: upload/download UI theo contract.

## Khi nào đọc
Thiết kế quản lý tệp, upload/download, đổi key, phát hành tài liệu hoặc backup/restore.

## Quy tắc
- **OBJ-01** — Cloudflare R2 private là kho tệp kết quả và tài liệu. Không tự thêm provider storage khác; không lấy bài học MinIO làm quyết định đổi kho. SQL Server giữ metadata/owner/liên kết hồ sơ-chỉ định/object key/trạng thái phát hành; R2 giữ bytes. Bucket y tế private; key ngẫu nhiên không chứa tên/CCCD.
- **OBJ-02** — Upload và download đi qua Backend proxy/stream: .NET kiểm quyền object-level, trạng thái Published và đúng tuyến (Staff qua Access; bệnh nhân chỉ nội dung đã phát hành thuộc quyền). Presigned URL R2 cho người giữ URL thao tác đến khi hết hạn nên không phải bằng chứng quyền hay mạng viện, không dùng làm cơ chế kiểm soát truy cập. Intent→upload→server xác minh size/type/nội dung/checksum→liên kết→duyệt/phát hành. Không tin Content-Type hoặc ghi đè file đã phát hành; đánh giá memory/bandwidth/body/timeout của proxy.
- **OBJ-03** — Không gọi R2 trong transaction DB. Upload lỗi giữ metadata nháp và gửi lại đúng ResultId, không tạo chỉ định hoặc vé mới. Move/rename là copy rồi delete có kiểm chứng; đối soát tệp mồ côi hoặc metadata thiếu tệp là tác vụ nền. Không log URL ký/token đầy đủ.
- **OBJ-04** — Xác minh API S3-compatible, SDK, giới hạn kích thước, quota và chi phí R2 trước pin. Credential R2 chỉ ở secret runtime của Backend/worker. Backup/restore gồm objects, DB, metadata mapping và cấu hình cần thiết.

## Điều cấm
Không access/secret key trong frontend, app mobile, repo hoặc image. Không bật public bucket hay custom domain công khai cho dữ liệu y tế. Không suy Cloudflare proxy có thể phục vụ mọi kích thước/timeout.

## Cách kiểm chứng
Test quyền A/B, kết quả Draft không tải được, file lớn/sai magic bytes, upload dở/orphan, copy lỗi không delete nguồn, restore DB cùng objects đọc được tài liệu giả. Test checksum tải xuống và metadata đúng lượt khám. Kiểm không có đường tải trực tiếp từ R2 mà không qua Backend.

## Nguồn
Giải pháp kỹ thuật v2.1 §6.4, §8, §8.1.3, §8.6 [T12]; Buổi 9 tr.1–4 (kiến thức object storage).
