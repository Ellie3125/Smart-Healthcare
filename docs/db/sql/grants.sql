-- Phân quyền tách tài khoản migrate và runtime (CLA-DB-001, DB-03).
-- Chỉ để review. Không chứa login hay mật khẩu: việc tạo login/user và cấp secret thuộc hạ tầng (Codex).
-- Chạy trong database ứng dụng sau khi DbMigrator đã tạo 6 schema.
-- Gán user: ALTER ROLE [sh_migrator] ADD MEMBER [<user migrate>]; ALTER ROLE [sh_runtime] ADD MEMBER [<user runtime>];

IF DATABASE_PRINCIPAL_ID(N'sh_runtime') IS NULL CREATE ROLE [sh_runtime];
IF DATABASE_PRINCIPAL_ID(N'sh_migrator') IS NULL CREATE ROLE [sh_migrator];

-- Runtime (BE1/BE2, worker): chỉ DML trên 6 schema module, không DDL.
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[identity]   TO [sh_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[catalog]    TO [sh_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[scheduling] TO [sh_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[clinical]   TO [sh_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[billing]    TO [sh_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[workflow]   TO [sh_runtime];

-- Migrator: tạo/sửa bảng trong 6 schema và ghi __EFMigrationsHistory của từng schema.
-- CREATE SCHEMA cần ở lần chạy đầu; sau khi 6 schema đã có có thể thu hồi quyền này.
GRANT CREATE TABLE, CREATE SCHEMA TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[identity]   TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[catalog]    TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[scheduling] TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[clinical]   TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[billing]    TO [sh_migrator];
GRANT ALTER, CONTROL ON SCHEMA::[workflow]   TO [sh_migrator];
-- EF Core 9+ lấy khóa migration bằng sp_getapplock (session); quyền EXECUTE mặc định của public là đủ.
