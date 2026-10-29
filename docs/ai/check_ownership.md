# Agent tự kiểm phạm vi thay đổi

Thực hiện trước khi ghi file, cuối phiên và trước bàn giao/PR. Đọc ownership.md và task hiện tại; không cần script kiểm tra riêng.

1. Xác định đúng agent từ công cụ đang dùng; ghi task ID, owner, allowed_paths, branch và commit nền đã review. Không dùng tên branch hoặc nhãn agent tự khai làm bằng chứng duy nhất khi review.
2. Xem git status và diff so với commit nền, gồm thay đổi đã commit sau nền, đã stage, chưa stage và file mới. Không chỉ xem diff cuối cùng nếu thay đổi đã stage có thể bị phần chưa stage che khuất.
3. Đối chiếu từng đường dẫn với ownership.md và allowed_paths. Quyền task chỉ thu hẹp phạm vi, không mở rộng owner. File chưa có owner thì xác định owner trước sửa.
4. Kiểm cả nguồn và đích của rename/move, file bị xóa, lockfile, file sinh tự động và thay đổi formatter. Symlink hoặc thay đổi có thể ghi ra ngoài phạm vi phải được review riêng.
5. Review nội dung: Codex không tạo/sửa/xóa/format UI ở bất kỳ đường dẫn nào; Antigravity không sửa server/DB/chatbot/hạ tầng/CI. Claude Code không sửa UI web/mobile, tests/e2e, backend, file compose (kể cả service chatbot/ollama), .env.example, docs/api hay hạ tầng ngoài infrastructure/cloudflare, trừ thay đổi theo OWN-10 có task CLA và handoff báo owner; Codex và Antigravity không sửa ml-service, tests/ml, docs/ml, docs/security, infrastructure/cloudflare, CLAUDE.md hay .claude/**. Không chuyển tên/thư mục để né ranh giới.
6. Nếu vượt scope, không tiếp tục sửa thay bên kia; giữ công việc thành viên khác, tách phần thuộc task mình và tạo handoff đúng chiều. Không sửa ownership để làm thay đổi đang vi phạm trở thành hợp lệ.
7. Ghi kết quả review: đã xem những file nào, baseline nào, có/không vi phạm, phần cần bàn giao và next_action. Nếu chưa xem đủ diff, ghi chưa kiểm chứng.

Đây là hướng dẫn review cho agent/người dùng, không tự chặn ghi file hoặc tự fail pipeline. Kiểm build/test nghiệp vụ vẫn theo testing-and-evidence.md và cấu hình ứng dụng thực tế.
