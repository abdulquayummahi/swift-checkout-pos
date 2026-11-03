# Upgrade Guide

## Normal upgrade (shop PC already running this version or later)

1. Close the shift if possible. As Admin: **Backup → Back up now** (must show *Verified*) and **Copy newest** to USB.
2. Administrator prompt in `C:\Program Files\SwiftCheckoutPOS`: `.\SwiftCheckoutPOS.UI.exe --restore-drill` → "Passed" (PR-Q84).
3. In the new package folder: `powershell -ExecutionPolicy Bypass -File .\install.ps1` (stops the service, copies the program,
   keeps all data and settings).
4. `cd "C:\Program Files\SwiftCheckoutPOS"; .\SwiftCheckoutPOS.UI.exe --migrate`
5. `Start-Service SwiftCheckoutPOS`, sign in, reprint the last receipt, open the Backup page.

### If something goes wrong
* Program does not start: read the newest file in `C:\ProgramData\SwiftCheckoutPOS\Logs`. Reinstall the previous package with
  `install.ps1` if needed.
* `--migrate` failed or data looks wrong: stop the service and restore the backup from step 1
  (`.\SwiftCheckoutPOS.UI.exe --restore <set name>`, [DATA_RECOVERY_PLAN.md](DATA_RECOVERY_PLAN.md) section 6), then reinstall
  the previous package.

## From the development versions before November 2026

The database history was restarted with one clean migration (`InitialBaseline`, IMM-03). A database created by the older
versions (for example the developer's `SwiftCheckoutPOSDb` with test data) **cannot be upgraded**; `--migrate` would try to
create tables that already exist. The owner confirmed that database holds only test data, so:

1. Back up the old database if you want to keep a copy (SQL Server Management Studio → Tasks → Back Up).
2. Delete it (SSMS → Delete) or point `DbConn` at a new name (e.g. `SwiftCheckoutPOS`).
3. `--migrate` creates the new empty database; the first page asks for the first Admin.

Old settings: a `backup_config.json` in the program folder is read once and kept; product images from the old
`wwwroot\images\products` folder keep working and are included in backups.

## Developer notes for a new release
* Add migrations with `dotnet ef migrations add <Name>` (startup project DAL); review the generated SQL with
  `dotnet ef migrations script`; never edit an applied migration.
* Run both test suites, then `deploy\build-package.ps1`.
* Note in the release text whether the upgrade needs `--migrate`.
