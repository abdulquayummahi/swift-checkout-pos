-- BP-06 / DATA_RECOVERY_PLAN.md section 8: least-privilege SQL access for the SwiftCheckoutPOS Windows service.
-- Run once as the owner (sysadmin) AFTER "SwiftCheckoutPOS.UI.exe --migrate" has created the database:
--   sqlcmd -S .\SQLEXPRESS -E -i sql-permissions.sql -v Database="SwiftCheckoutPOS" AppLogin="NT SERVICE\SwiftCheckoutPOS"
SET NOCOUNT ON;
IF SUSER_ID(N'$(AppLogin)') IS NULL CREATE LOGIN [$(AppLogin)] FROM WINDOWS WITH DEFAULT_DATABASE = [$(Database)];
GO
-- RESTORE VERIFYONLY (backup verification) needs CREATE DATABASE permission; it does not allow restoring over a database.
USE [master];
GRANT CREATE ANY DATABASE TO [$(AppLogin)];
GO
USE [$(Database)];
IF DATABASE_PRINCIPAL_ID(N'$(AppLogin)') IS NULL CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)];
ALTER ROLE db_datareader ADD MEMBER [$(AppLogin)];
ALTER ROLE db_datawriter ADD MEMBER [$(AppLogin)];
ALTER ROLE db_backupoperator ADD MEMBER [$(AppLogin)];
GO
PRINT N'Permissions granted. The service account is not sysadmin or db_owner; migrations and restores run from the owner console.';
