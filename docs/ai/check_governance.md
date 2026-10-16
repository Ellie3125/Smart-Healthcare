# Agent tự kiểm bộ quy chuẩn

Đọc trước khi sửa bộ hướng dẫn hoặc báo bộ hướng dẫn đã hoàn chỉnh. Thực hiện bằng cách đọc file và đối chiếu nội dung, không cần chạy Python.

1. Xác nhận AGENTS.md, GEMINI.md, .agents/rules/smart-healthcare.md, ownership.md, skill-manifest.md, các instructions và SKILL.md được tham chiếu đều tồn tại.
2. Kiểm mỗi instruction có phạm vi, thời điểm đọc, quy tắc, điều cấm, cách kiểm chứng và nguồn. Mỗi rule ID có một nơi định nghĩa chính; không trùng ID hoặc duy trì hai luật trái nhau.
3. Đối chiếu stack hiện hành xuyên suốt: .NET/SQL Server, React/Vite, MinIO chính, Nginx serve Frontend/proxy API, Jenkins, ML nội bộ, RabbitMQ; VPN tùy chọn. Không dùng file phụ để ghi đè một luật lỗi thời còn hoạt động.
4. Kiểm scope Codex/Antigravity thống nhất trong AGENTS, GEMINI, rule nạp, ownership, instructions và skills. Skill không cấp quyền sửa vượt scope.
5. Kiểm đường dẫn/link trong hướng dẫn và license của từng skill. Tài nguyên không có trong gói không được mô tả như đã cài hoặc bắt buộc chạy.
6. Khi cập nhật skill, đọc diff, nguồn commit, license và nội dung điều chỉnh; cập nhật danh mục skill trực tiếp. Không tự update giữa task tính năng.
7. Báo rõ file đã đọc, lỗi phát hiện và phần chưa xác minh. File có trên ổ đĩa không chứng minh IDE đã nạp; chỉ ghi activated sau khi có bằng chứng trong phiên công cụ thực tế.

Checklist này chỉ kiểm hướng dẫn. Nó không xác nhận build/test ứng dụng, nghiệp vụ hay triển khai đã thành công, và không tự tạo kiểm tra CI.
