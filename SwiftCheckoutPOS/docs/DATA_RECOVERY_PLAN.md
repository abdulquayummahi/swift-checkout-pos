# SwiftCheckoutPOS Data Recovery Plan

Status: **implemented** by IMM-10 / BP-05 / BP-12 (branch T08-T10-backup-hosting, 2026-11-04). Integration-tested on LocalDB
`SwiftCheckoutPOS_Test`; not yet measured on the shop PC (see section 9). This is the single recovery document.

## 1. Approved objectives (OWNER_DECISIONS.md)

| Item | Requirement | How it is met |
| --- | --- | --- |
| RPO (PR-Q69) | At most one business day of sales lost | Daily verified backup; the Backup page warns when the newest verified backup is older than 26 hours |
| RTO (PR-Q54) | Selling again within 2 hours | `--restore` (section 6) takes minutes on a small-shop database; measure it in the first drill |
| Automatic backup (PR-Q53/Q89) | Daily 23:00 with catch-up | `AutoBackupWorker` checks every minute; due when no verified backup exists since the most recent 23:00, so a PC that was off runs it at the next start |
| Pre-upgrade backup (PR-Q53) | Manual, before every upgrade | `SwiftCheckoutPOS.UI.exe --backup` (kind *PreUpgrade*) or **Back up now** |
| Retention (PR-Q55) | 30 daily + 12 monthly, never the newest verified | Section 4 |
| Separate copy (PR-Q56) | Daily USB, kept away from the PC | Automatic when the configured USB drive is plugged in, or **Copy newest**; the copy's checksum is checked |
| Files kept with backups (PR-Q57) | Product images and settings | `Name.files.zip` beside each `.bak` |
| Restore drill (PR-Q84) | Monthly and before every upgrade | `--restore-drill` (section 5) |
| Who | Admin only (PR-Q50/Q92) | Backup page, downloads and USB copy are Admin-only even when Manager financial view is ON; restore is an owner console command |

## 2. Where things are

All shop data files live under the **data root** `C:\ProgramData\SwiftCheckoutPOS` (setting `Paths:DataRoot`), never in the
program folder, so reinstalling or upgrading the program cannot delete them:

| Folder | Contents |
| --- | --- |
| `Backups\` (default; Admin can choose another local folder) | Backup sets |
| `Config\` | `appsettings.Local.json` (machine settings), `backup_config.json`, `backup_status.json` (last USB copy / drill) |
| `Media\products\` | Product images uploaded by this version (older images stay in the program's `wwwroot\images\products` and are also archived) |
| `Keys\` | Login-cookie key ring (DPAPI, this PC only — not needed for recovery; after a new PC everyone just signs in again) |
| `Logs\` | Daily log files, 30 kept |

A **backup set** is three files with the same name, e.g. `SwiftCheckoutPOS_20261104_230000_Daily`:

* `.bak` — native SQL Server full backup made `WITH CHECKSUM`, then checked with `RESTORE VERIFYONLY WITH CHECKSUM`.
* `.files.zip` — product images (`Media/`, `LegacyImages/`) and settings (`Config/`).
* `.json` — manifest: database, time, kind (Daily / Manual / PreUpgrade / PreRestore), who, size, SHA-256 of both files,
  verified flag or the failure message. The list of backups comes from these manifests, not from the database being protected.

Backups contain customer data and password hashes: keep the USB drive locked away.

## 3. Safety rules in the code

* Database names and file paths are SQL parameters (`BACKUP DATABASE @db TO DISK = @path`); `ALTER/DROP DATABASE` use `QUOTENAME`.
  No typed text is ever joined into SQL.
* The backup folder must be a full local path (no network share, no wildcards) outside the program folder; saving the setting
  tests that the folder can be written. A pre-upgrade `backup_config.json` in the program folder is read once and left in place;
  if its folder was inside the program folder the default is used and the page shows where the old backups are.
* Downloads accept only exact backup file names (`SwiftCheckoutPOS_yyyyMMdd_HHmmss_Kind.bak|.files.zip`, or an older
  `SwiftCheckoutPOS_Backup_yyyyMMdd_HHmmss.bak`) that resolve inside the backup folder. Every download is audited.
* A failed backup deletes its partial files, writes a manifest with the error, logs it and retries after 15 minutes.
* Product images (IMM-10): JPG/PNG/WebP checked by content, ≤ 2 MB, ≤ 2000 × 2000 pixels, saved under a generated name;
  the old image is deleted only after the product is saved, by exact name, so a failed upload keeps the old picture.

## 4. Retention (PR-Q55)

After every successful backup, in the backup folder (and on the USB drive after each copy):

1. The newest verified set is always kept.
2. Every verified set from the **30 most recent days that have a backup** is kept (a manual and the 23:00 backup of the same day are both kept).
3. The **first** verified set of each of the **12 most recent months** is kept.
4. Other verified sets are deleted (all three files). Failure records older than 30 days are removed.
5. Older `.bak` files without a manifest (from before this version) are never deleted automatically.

The PreRestore safety backup made by `--restore` skips retention for that run.

## 5. Monthly restore drill (PR-Q84)

Run monthly and before every upgrade, in an **administrator** command prompt in the program folder (it uses your Windows login
on SQL Server, which needs permission to create databases):

```
SwiftCheckoutPOS.UI.exe --restore-drill            (newest verified set)
SwiftCheckoutPOS.UI.exe --restore-drill <set name> (a specific set)
```

It checks the `.bak` checksum against the manifest, runs `RESTORE VERIFYONLY`, restores it as a separate database
`<database>_RestoreDrill`, runs `DBCC CHECKDB`, counts products, sales and users, then drops the drill database. Live data is not
touched and the shop can keep selling. The result and date appear on the Backup page; a failed drill is shown as a warning.

## 6. Real restore (owner only)

Use only when the live database is lost or damaged. Restoring replaces everything since that backup.

1. Stop the program: `sc stop SwiftCheckoutPOS` (or close the console window if run by hand).
2. Choose the set: newest verified one in `C:\ProgramData\SwiftCheckoutPOS\Backups` (or copy a set from the USB drive's
   `SwiftCheckoutPOS-Backups` folder into the backup folder first — all three files).
3. In an administrator prompt in the program folder: `SwiftCheckoutPOS.UI.exe --restore <set name>` and type the database name to confirm.
   The command checks the checksum and `VERIFYONLY`, makes a **PreRestore** safety backup of the current data, closes other
   connections (`SINGLE_USER WITH ROLLBACK IMMEDIATE`), restores onto the database's existing files, and always returns it to
   `MULTI_USER`. If the restore fails, the safety backup is put back automatically and the message says so.
4. If the backup is from an older program version: `SwiftCheckoutPOS.UI.exe --migrate`.
5. Images/settings: if they were lost too, unzip the set's `.files.zip` — `Media\` and `Config\` into the data root,
   `LegacyImages\` into the program's `wwwroot\images\products`.
6. Start the program (`sc start SwiftCheckoutPOS`), sign in, open the last receipt (Reprint, blank) and the till to confirm.
   Make a new backup (**Back up now**) once satisfied.

New PC: install SQL Server Express and the program (DEPLOYMENT_GUIDE.md), copy the set from USB into the new backup folder, then
steps 3–6 (the database is created by the restore). Everyone signs in again because cookie keys are per PC.

## 7. Crash or power cut

SQL Server recovers committed work itself on restart. Sales are single database transactions with an operation key, so a sale is
either saved or not; held orders and reservations are in the database. After the PC restarts, the program starts with Windows,
the scheduled backup catches up if 23:00 was missed, and the Backup page shows the backup age.

## 8. Permissions

* SQL Server service account (e.g. `NT SERVICE\MSSQL$SQLEXPRESS`) writes the `.bak`: give it *Modify* on the backup folder
  (`deploy\install.ps1` does this for the default folder; repeat it for a folder you choose).
* The program's service account needs read/write on its database, `db_backupoperator`, and `CREATE ANY DATABASE` only because
  `RESTORE VERIFYONLY` requires it. It does **not** get sysadmin or restore-over-existing rights; restore and drills run in the
  owner's console with the owner's Windows login (`deploy\sql-permissions.sql`).

## 9. Evidence and open items

* Integration tests (`Tests/.../Recovery/BackupTests.cs`): verified set + manifest + files zip, USB copy with checksum, retention
  rules, schedule/catch-up, settings validation and legacy file, download name containment, image validation, drill on a
  separate database, in-place restore with safety backup and checksum tamper refusal.
* Still to do on the shop PC: run `--restore-drill` and one timed restore to measure RTO against 2 hours; confirm the folder
  permissions with the real SQL service account. PR-Q63 capacity remains PENDING (no records are ever deleted to save space).
* Known limit: the images/settings archive is taken right after the SQL backup, not inside a pause, so an image uploaded in the
  same minute may be in one and not the other. Acceptable for a one-PC shop; the product row keeps working with the default image.
