# Session FR01-account-profile s1

| Trường | Giá trị |
|---|---|
| Công cụ | Claude Code (Opus 5.5), trong VS Code |
| Thời gian | 29/10/2026 |
| Branch / base / HEAD | feature/fr01-account-profile / 3d7030c / 3d7030c (thay đổi chưa commit) |
| Contract | docs/api/fr01-account-profile.md v0.1 Draft |

## Ủy quyền
Chủ repo chọn trong phiên: "Viết thẳng vào repo", "Backend API trước", "EF Core migration theo module". Theo docs/ai/ownership.md, backend/** thuộc Codex; Claude Code không phải agent được liệt kê, nên thay đổi này cần trưởng nhóm review trước khi merge.

## Đã đọc
AGENTS.md, docs/ai/ownership.md và các instruction: project-governance, codex-scope, backend-solid-architecture, sqlserver-efcore, database-module-boundaries, api-contract-and-handoff, testing-and-evidence, healthcare-domain. Tài liệu chủ repo cung cấp: Phạm vi nghiệp vụ v2.1, Giải pháp kỹ thuật v2.1, Smart Healthcare ERD v2.2.html. Không dùng skill.

## Thay đổi
- Solution `backend/SmartHealthcare.slnx`; các project Api, SharedKernel; Identity (Domain, Application, Infrastructure, Presentation, Contracts); Patients (Domain, Application, Infrastructure, Presentation).
- Migration `InitialIdentity`, `InitialPatients`; local tool `dotnet-ef` 10.0.14 (`backend/dotnet-tools.json`).
- Test: `tests/backend/Unit`, `tests/backend/Architecture`, `tests/integration`.
- Đã xóa các `.gitkeep` ở thư mục nay đã có project. Đã thêm `.gitignore` riêng trong `backend/`, `tests/backend/`, `tests/integration/`, vì repo chưa có `.gitignore` gốc (file gốc thuộc Codex).
- Database `SmartHealthcare` trên localhost: thay 35 bảng cũ bằng 4 bảng FR01 qua migration.
- Ngoài repo: đặt user-secrets cho Api trên máy dev (`Jwt:SigningKey` sinh ngẫu nhiên, `ConnectionStrings:SmartHealthcare`).

## Lệnh và kết quả
Xem docs/evidence/codex/FR01-test-run.md: build exit 0; unit 21/21; architecture 20/20; integration 18/18 trên SQL Server; smoke trên Kestrel đạt.

## Chưa làm / chưa kiểm chứng
- Idempotency-Key (DB-04), refresh token, xác minh email và số điện thoại, khóa tài khoản sau nhiều lần sai.
- Chưa có `global.json` và `Directory.Packages.props` ở gốc repo (file gốc thuộc Codex). SDK chưa được ghim; mỗi csproj ghi version package riêng.
- Chưa có UI và E2E; chưa chạy hai replica sau Nginx.

## next_action
Trưởng nhóm review branch và các điểm lệch ERD trong task file; Antigravity phản hồi contract v0.1.
