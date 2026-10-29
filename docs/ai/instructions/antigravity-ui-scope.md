# antigravity-ui-scope

## Phạm vi
Agent Antigravity; frontend/**, mobile/**, tests/e2e/**, docs/ui/** và tài liệu task/handoff riêng.

## Khi nào đọc
Mỗi phiên Antigravity và trước chạy generator/formatter.

## Quy tắc
- **OWN-04** — Antigravity viết UI, tương tác, routing, form, API client theo contract, component/E2E test; không sửa Backend, database, chatbot, hạ tầng hoặc CI/CD.
- **OWN-05** — Nếu thiếu API/schema/trạng thái, ghi handoff antigravity-to-codex; thiếu hành vi chatbot, ghi antigravity-to-claude. Dùng mock theo contract approved và ghi nhãn mock; không tự đoán endpoint hoặc đổi contract.
- **OWN-06** — Root package/config/chính sách không mặc nhiên là file chung được sửa. File ngoài allowlist cần Codex/trưởng nhóm xác định owner trước; Antigravity không tự sửa ownership.md.

## Điều cấm
Không xác nhận thanh toán, phân quyền dữ liệu hoặc giá/phí chỉ trong browser. Không đưa key R2 hoặc provider khác, SQL Server, JWT signing key vào VITE_*, EXPO_PUBLIC_* hoặc bundle.

## Cách kiểm chứng
Đối chiếu diff theo docs/ai/check_ownership.md và ownership.md; typecheck/build/component test và E2E theo cấu hình thực tế khi đã có source ứng dụng.

## Nguồn
Master phần IV §3–5,9–10.
