-- Chỉ dùng cho môi trường dev (compose.dev.yaml, service db-init). Chạy bằng sqlcmd với identity quản trị.
-- Tạo DB, login runtime riêng cho Api/Worker (không dùng sa trong runtime), và schema hangfire do login runtime sở hữu
-- để Hangfire tự tạo bảng của nó. Bảng nghiệp vụ do migrator tạo, login runtime chỉ đọc/ghi dữ liệu.
-- Biến sqlcmd: DB_NAME, APP_LOGIN, APP_PASSWORD (mật khẩu không chứa dấu nháy đơn).
SET NOCOUNT ON;

IF DB_ID(N'$(DB_NAME)') IS NULL
    CREATE DATABASE [$(DB_NAME)];
GO

IF SUSER_ID(N'$(APP_LOGIN)') IS NULL
    CREATE LOGIN [$(APP_LOGIN)] WITH PASSWORD = N'$(APP_PASSWORD)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DB_NAME)];
ELSE
    ALTER LOGIN [$(APP_LOGIN)] WITH PASSWORD = N'$(APP_PASSWORD)';
GO

USE [$(DB_NAME)];
GO

IF USER_ID(N'$(APP_LOGIN)') IS NULL
    CREATE USER [$(APP_LOGIN)] FOR LOGIN [$(APP_LOGIN)];
ALTER ROLE db_datareader ADD MEMBER [$(APP_LOGIN)];
ALTER ROLE db_datawriter ADD MEMBER [$(APP_LOGIN)];

IF SCHEMA_ID(N'hangfire') IS NULL
    EXEC (N'CREATE SCHEMA [hangfire] AUTHORIZATION [$(APP_LOGIN)]');
-- CREATE TABLE chỉ có tác dụng trong schema mà login sở hữu, tức là schema hangfire.
GRANT CREATE TABLE TO [$(APP_LOGIN)];
GO
