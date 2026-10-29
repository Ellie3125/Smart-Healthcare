# testing-and-evidence

## Phạm vi
Cả ba agent (Codex, Antigravity, Claude Code); test scope thuộc owner tương ứng.

## Khi nào đọc
Trước báo hoàn thành, bàn giao, PR hoặc release.

## Quy tắc
- **QA-01** — Chỉ báo pass khi đã chạy lệnh tương ứng trên commit/diff hiện tại; ghi cwd, command, exit code, thời gian, môi trường, dữ liệu giả, result path và limitations. Không suy build pass từ lint pass.
- **QA-02** — Phân biệt static check, unit/mock, SQL Server integration, provider sandbox thật, UI mock, UI API thật, chatbot với API giả/thật và model thật, E2E; mọi tầng chưa chạy ghi Not run/Blocked, không Passed.
- **QA-03** — Mỗi requirement gắn Use Case→contract/module→test ID→evidence. Ưu tiên boundary, concurrency, auth, payment và recovery. Sửa bug có reproduction/regression phù hợp; không viết test chỉ lặp implementation.
- **QA-04** — Done xuyên suốt cần Backend test nghiệp vụ + UI đủ trạng thái + API thật + E2E quan trọng + tài liệu/bằng chứng. Hoàn thành Backend không đồng nghĩa UI done.
- **QA-05** — Scope check dùng diff đầy đủ gồm add/delete/rename/untracked; CI dùng base policy và agent identity do hệ thống review cấp, không lấy agent tự khai trong file PR. Review nội dung để bắt UI giấu ngoài frontend.

## Điều cấm
Không fake log/screenshot/metrics; không xóa volume hay dữ liệu khi test chưa được ủy quyền. Checklist check_governance.md chỉ kiểm bộ hướng dẫn, không thay kiểm thử ứng dụng.

## Cách kiểm chứng
Dùng verification-before-completion; lấy lệnh từ cấu hình source hiện có; nếu chưa có ứng dụng thì ghi chưa có lệnh build/test, không tự bịa. Evidence mỗi task thuộc owner riêng, không sửa báo cáo của agent kia.

## Nguồn
Master I §13,18–19, III QA-01/02, IV §10; Buổi 10 tr.2–5.
