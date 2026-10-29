# Session FR01-account-profile s3

| Trường | Giá trị |
|---|---|
| Công cụ | Claude Code (Opus 5.5), trong VS Code |
| Thời gian | 29/10/2026 |
| Branch / base / HEAD | feature/fr01-account-profile / 3d7030c / 3d7030c (thay đổi chưa commit) |
| Contract | docs/api/fr01-account-profile.md v0.1 Draft, API không đổi |

## Yêu cầu
Chủ repo hỏi database có tạo bằng migration không (lúc đó: không, database-first), rồi yêu cầu "tôi muốn dùng migration".

## Thay đổi
- Thêm project `backend/src/Database` (`SmartHealthcare.Database`), đã thêm vào `SmartHealthcare.slnx`:
  - `SchemaDbContext` mô tả 46 bảng dạng property bag, giữ đúng tên mọi ràng buộc như script cũ.
  - Migration `InitialSchema`.
  - `README.md` hướng dẫn thêm và áp migration.
- Khôi phục `backend/dotnet-tools.json` (dotnet-ef 10.0.14).
- Xóa `infrastructure/sqlserver/schema/SmartHealthcare_v2.2.sql`; migration thay thế script.
- Database `SmartHealthcare_ERD` (0 dòng dữ liệu): xóa rồi tạo lại bằng `dotnet ef database update`.
- Tests:
  - `ApiFactory` tạo DB tạm bằng `MigrateAsync`.
  - `SchemaScriptTests` đổi thành `SchemaTests`, thêm kiểm tra không còn migration chưa áp và không có thay đổi mô hình thiếu migration.
  - Architecture: thêm test API và module không tham chiếu project migration.
- DbContext của module Identity và Patients không đổi.

## Lệnh và kết quả
Xem docs/evidence/codex/FR01-test-run.md, mục "Lần 3".

## Chưa làm / chưa kiểm chứng
- Lệch MOD-01/MOD-04 (một bộ migration chung trong `dbo`, có FK xuyên module) ghi trong task file; chưa có ADR.
- Chưa thử áp script idempotent bằng sqlcmd; database `SmartHealthcare` cũ (4 bảng, migration module cũ) vẫn còn trên máy dev.

## next_action
Trưởng nhóm duyệt cách tổ chức migration và ADR; FR tiếp theo thêm thay đổi schema bằng `dotnet ef migrations add` trong `backend/src/Database`.
