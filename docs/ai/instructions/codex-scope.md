# codex-scope

## Phạm vi
Agent Codex; mọi lệnh shell, chỉnh sửa, tạo/xóa/format/generate.

## Khi nào đọc
Mỗi phiên Codex và trước thực thi công cụ ghi file.

## Quy tắc
- **OWN-01** — Codex không được tạo, sửa, xóa hoặc định dạng frontend/** hay mã UI ở bất kỳ đường dẫn nào; tests/e2e/** và docs/ui/** thuộc Antigravity. Có thể đọc để chẩn đoán.
- **OWN-02** — Codex sở hữu backend, ml-service, infrastructure, scripts, tests/backend, tests/integration, tests/ml, tests/load, contract và tài liệu nghiệp vụ/kiến trúc. Quyền file chính xác nằm ở ownership.md.
- **OWN-03** — Nếu phát hiện lỗi UI, tạo handoff codex-to-antigravity gồm tái hiện, contract, ảnh hưởng và acceptance criteria. Tiếp tục phần Backend độc lập; báo UI pending, không sửa hộ.

## Điều cấm
Không có ngoại lệ sửa nhỏ, đổi lockfile, sửa API client sinh tự động hoặc sửa UI để build xanh. Không chuyển mã UI sang thư mục khác để tránh kiểm tra.

## Cách kiểm chứng
Đối chiếu toàn bộ diff theo docs/ai/check_ownership.md và ownership.md; review cả nội dung để phát hiện UI đặt sai đường dẫn.

## Nguồn
Master phần IV §2,4,6,9.
