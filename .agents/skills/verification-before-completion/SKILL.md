---
name: verification-before-completion
description: "Kiểm chứng bằng lệnh và bằng chứng trước khi báo hoàn thành, sửa xong, test pass hoặc bàn giao công việc."
license: "Complete terms in LICENSE.md"
---

# Kiểm chứng trước khi hoàn thành

1. Xác định claim cần chứng minh: sửa bug, build, test, integration, UI hoặc release.
2. Chọn lệnh/kiểm tra thực tế từ repository, đúng cwd và môi trường. Kiểm không vượt scope của agent; nếu chưa có code/công cụ thì ghi Not run/Blocked.
3. Chạy kiểm tra cần thiết trên trạng thái code hiện tại; đọc toàn output liên quan và exit code. Lint không chứng minh build, unit mock không chứng minh provider/SQL Server/E2E.
4. Nếu fail: xác định nguyên nhân, sửa trong scope và kiểm lại phần bị ảnh hưởng. Không làm sạch hoặc revert thay đổi của thành viên khác để test xanh.
5. Với bug, kiểm cách tái hiện/regression; chỉ dùng red-green/revert trên thay đổi riêng trong worktree task khi an toàn. Không yêu cầu viết test máy móc cho chỉnh sửa thấp tác động.
6. Đối chiếu acceptance criteria, quyền hồ sơ, dữ liệu/migration và handoff cần thiết. Ghi chính xác phần chưa làm hoặc chưa kiểm chứng.
7. Cập nhật task/session/evidence theo project-governance.md: command, cwd, exit code, môi trường, kết quả và next_action. Báo BE/UI-mock/UI-real/E2E riêng theo trạng thái thật.

Không tuyên bố pass từ suy đoán, output cũ sau thay đổi liên quan, lời agent khác hoặc screenshot mock. Không dùng một test pass để tuyên bố đáp ứng toàn học phần hay production.

Nguồn: https://github.com/obra/superpowers/tree/bb92a77741419a4ab5f06e711a283343f1ada0c3/skills/verification-before-completion. Bản hướng dẫn đã được biên tập cho Smart Healthcare, ngày 16/10/2026; giữ license tác giả trong LICENSE.md.
