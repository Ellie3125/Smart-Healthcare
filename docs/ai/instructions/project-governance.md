# project-governance

## Phạm vi
Toàn repository; Codex quản lý quy chuẩn và contract server, trưởng nhóm review thay đổi chính sách.

## Khi nào đọc
Đầu phiên, trước lập kế hoạch, thêm dependency hoặc đổi thiết kế.

## Quy tắc
- **GOV-01** — Đọc AGENTS.md, instructions liên quan, task/contract/thiết kế hiện có và git status trước khi sửa. Báo agent, branch, base commit, task ID và nguồn thực sự đã đọc. Không giả tài liệu chưa có đã được phê duyệt.
- **GOV-02** — Nguồn sự thật là docs/business/pham-vi-nghiep-vu-v2.1.md rồi docs/architecture/giai-phap-ky-thuat-v2.2.md; luật trái hai file này là luật lỗi thời, sửa theo GOV-06. Giữ .NET/ASP.NET Core/EF Core/SQL Server; Modular Monolith + Clean Architecture trong từng module, hai replica BE1/BE2 cùng image và worker .NET riêng chạy Hangfire + SQL Server với Operation + Outbox (MVP chưa có message broker); web React/Vite/TypeScript, app bệnh nhân React Native + Expo; chatbot Hướng 2 bằng Python/FastAPI + LangChain `create_agent` + Ollama trong VPS, giai đoạn 1 dùng model nền chưa fine-tune; Cloudflare R2 private cho tệp, Backend kiểm quyền rồi proxy/stream; thanh toán VNPT/MoMo; Docker Compose base + override dev/staging/prod (Dev trên laptop, Staging và Prod trên hai VPS Ubuntu riêng), Nginx; Cloudflare DNS/proxy/TLS, Access và Tunnel; Jenkins. Nginx phục vụ Frontend build và proxy API cùng origin. VPN riêng tùy chọn, chưa chọn công nghệ. Chốt phiên bản/edition/license tương thích bằng quyết định thiết kế và lockfile trước triển khai.
- **GOV-03** — Mỗi task ghi task_id, owner_agent, owner_member, base_commit, branch, requirement_ids, allowed_paths, contract_version, acceptance_criteria, status, depends_on và next_action. Scope sửa là giao của ownership và allowed_paths. Chưa có thiết kế/contract được chấp nhận thì làm thiết kế, chưa sinh toàn app.
- **GOV-04** — Formatter/generator/dependency updater cũng giữ scope. Không sửa/revert/ghi đè thay đổi của người khác. Đọc file dùng chung trước khi hợp nhất; file ngoài ownership allowlist phải xác định owner trước sửa.
- **GOV-05** — Mỗi thành viên/agent một branch và worktree riêng khi chạy song song; một task một người đang ghi. Feature→dev→staging→main. Không tự merge/push/deploy ngoài ủy quyền hiện có.
- **GOV-06** — Duy trì một bộ instructions hiện hành: khi có quyết định mới được chấp thuận, sửa trực tiếp các quy tắc bị ảnh hưởng, loại nội dung không còn áp dụng; Git lưu lịch sử. Không dùng file phụ để yêu cầu agent bỏ qua một luật trái ngược còn hoạt động. Không sửa chính sách để vượt quyền/gate.
- **GOV-07** — Lưu trạng thái/task/session/handoff theo agent trong repository; đầu phiên đọc trạng thái thật, cuối phiên ghi changed files, quyết định, test/evidence, blockers và next_action. Phân biệt BE/UI-mock/UI-real/E2E; không dựa trí nhớ chat làm nguồn duy nhất.
- **AI-01** — Chọn một skill chính theo AGENTS.md, thêm hỗ trợ khi cần; công việc không có skill dùng instructions + tài liệu chính thức. Không tự cài marketplace, chạy installer/hook hoặc gọi subagent chỉ vì skill đề cập.
- **AI-02** — Skill không cấp quyền công cụ hoặc tự đổi yêu cầu. Đọc hướng dẫn và nguồn liên quan trước áp dụng; không tự update skill giữa tính năng. Khi cập nhật có chủ đích, hợp nhất nội dung và cập nhật danh mục nguồn/license trong cùng thay đổi đã review.

## Tổ chức công việc và bàn giao
Chỉ tạo hồ sơ vận hành khi có task thật, không tạo trước hàng loạt file rỗng:
- Task: docs/tasks/<agent>/<task-id>.md; trạng thái Draft→Ready→In progress→In review→Done, hoặc Blocked với lý do. Phải có scope và acceptance criteria trước Ready.
- Session: docs/sessions/<agent>/<task-id>-<session-id>.md; ghi công cụ, ngày giờ, branch/base/HEAD, instructions/skills đã đọc, contract version, changed/dirty files, command/cwd/exit code, phần chưa kiểm chứng và next_action.
- Evidence: docs/evidence/<agent>/; chỉ log/output thật đã loại thông tin nhạy cảm.
- Handoff: docs/handoffs/<người gửi>-to-<người nhận>/ gồm codex-to-antigravity, antigravity-to-codex, claude-to-codex, codex-to-claude, claude-to-antigravity, antigravity-to-claude. Bên nhận trả lời trong thư mục của mình, không sửa handoff của bên gửi.
- Contract server: docs/api/, Codex sở hữu; quyết định thiết kế: docs/architecture/ hoặc docs/adr/ khi thực sự phát sinh.
Task file là hồ sơ công việc, không phải distributed lock; trưởng nhóm điều phối xung đột nhận task giữa các branch. Nếu tài liệu tương ứng chưa có, tạo phần cần thiết trong scope thay vì dựa vào mẫu không tồn tại.
Sửa lỗi theo vòng: tái hiện→log redacted→giả thuyết→thử thay đổi tối thiểu→fix nguyên nhân→regression. Không chồng workaround hoặc sửa ngoài scope.

## Điều cấm
Không tự đổi database/stack/hosting hoặc thêm cloud storage/dịch vụ trả phí/telemetry/gửi code ra ngoài. Không dùng skill UI để cấp Codex quyền UI. Không yêu cầu xác nhận lặp lại cho việc đã được ủy quyền; chỉ hỏi quyết định thực sự thiếu và ảnh hưởng phần đang làm.

## Cách kiểm chứng
Review task, contract và toàn bộ diff theo docs/ai/check_ownership.md; dùng baseline và chính sách đã được review, không dùng chính sách sửa trong PR để tự mở rộng quyền. Cuối phiên phải có next_action và trạng thái từng tầng đúng bằng chứng. Kiểm các file instructions không còn quy tắc công nghệ trái nhau.

## Nguồn
Phạm vi nghiệp vụ v2.1; Giải pháp kỹ thuật v2.2 §8; Prompt_Stack_Smart_Healthcare §2–3,10–11; Master phần III–IV; Buổi 11 tr.1–3. Nội dung yêu cầu đã được hợp nhất trực tiếp trong file này.
