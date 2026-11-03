# SwiftCheckoutPOS Deployment Guide (one shop PC, offline)

Implements BP-06 / T10. The till runs as a Windows service on the shop PC and is opened in a browser at
`http://localhost:5080`. It listens **only on this PC** (loopback); other computers on the network cannot reach it.
No internet connection is needed after installation.

## 1. What you need

* Windows 10/11 64-bit, an Administrator account.
* SQL Server 2022 Express (free) with the default instance name `SQLEXPRESS`, Windows authentication. Install it once from the
  offline installer; leave "SQL Server Agent" off (not needed).
* `sqlcmd` (comes with SQL Server tools) for one permission script.
* The install package folder `SwiftCheckoutPOS-<date>` (built with `deploy\build-package.ps1`, copied on USB). It is
  self-contained: the .NET runtime is inside, nothing else to install.

## 2. Where things go

| Path | What | Upgrade/reinstall |
| --- | --- | --- |
| `C:\Program Files\SwiftCheckoutPOS\` | Program | Replaced |
| `C:\ProgramData\SwiftCheckoutPOS\Config\appsettings.Local.json` | Machine settings: connection string, port, printer | Kept |
| `C:\ProgramData\SwiftCheckoutPOS\Backups\` | Backups | Kept |
| `C:\ProgramData\SwiftCheckoutPOS\Media\products\` | Product images | Kept |
| `C:\ProgramData\SwiftCheckoutPOS\Keys\` | Login cookie keys (DPAPI, this PC) | Kept |
| `C:\ProgramData\SwiftCheckoutPOS\Logs\` | Daily logs, 30 days | Kept |

Only SYSTEM, Administrators and the service account (`NT SERVICE\SwiftCheckoutPOS`) can open the data folder.

## 3. First installation

In an **Administrator PowerShell**, inside the package folder:

```
powershell -ExecutionPolicy Bypass -File .\install.ps1
cd "C:\Program Files\SwiftCheckoutPOS"
.\SwiftCheckoutPOS.UI.exe --migrate
sqlcmd -S .\SQLEXPRESS -E -i "<package>\sql-permissions.sql" -v Database="SwiftCheckoutPOS" AppLogin="NT SERVICE\SwiftCheckoutPOS"
Start-Service SwiftCheckoutPOS
```

Then double-click **SwiftCheckoutPOS** on the desktop. On a new empty database the first page asks you to create the first
Admin (BP-13). Next, as Admin:

1. **Settings** — receipt header (shop name, address, phone, footer). Leave the financial view for Managers OFF unless you want it.
2. **Backup** — check the folder, keep automatic backup ON at 23:00, set the USB drive letter, press **Back up now**, then
   **Copy newest** with the USB drive plugged in.
3. Create Manager/Cashier users, products, then open a shift and make a test sale.

`install.ps1` options: `-SqlInstance`, `-Database`, `-Port`, `-InstallDir`, `-DataRoot`, `-SqlServiceAccount` (if SQL Server
runs under another account). It creates `appsettings.Local.json` only when it does not exist yet.

## 4. Machine settings (`appsettings.Local.json`)

```json
{
  "ConnectionStrings": { "DbConn": "Server=.\\SQLEXPRESS;Database=SwiftCheckoutPOS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True" },
  "Hosting": { "Port": 5080 },
  "Printer": { "Mode": "Virtual" }
}
```

* Without `DbConn` the program does not start and prints which file to create (exit code 2). Never put this file in Git.
* Printer (BP-08), after the printer is tested: `"Printer": { "Mode": "EscPosSerial", "Port": "COM3", "BaudRate": 9600 }` or
  `"Printer": { "Mode": "EscPosWindows", "Name": "<Windows printer name>" }`. Restart the service after a change.
* Optional HTTPS on loopback: `"Hosting": { "Port": 5443, "CertificatePath": "C:\\ProgramData\\SwiftCheckoutPOS\\Config\\pos.pfx", "CertificatePassword": "..." }`.
  Plain HTTP is acceptable here because traffic never leaves the PC; HTTPS only adds a certificate to manage.
* `Paths:DataRoot` moves the whole data root (rarely needed).

## 5. Service behaviour

* Starts automatically (delayed start, after SQL Server), restarts itself after a crash (1 min, 1 min, 5 min).
* `Stop-Service SwiftCheckoutPOS` / `Start-Service SwiftCheckoutPOS`; status in `services.msc`.
* Logs: `C:\ProgramData\SwiftCheckoutPOS\Logs\swiftcheckout-yyyyMMdd.log`. They contain no passwords or connection strings.
* Login sessions survive a restart (persisted keys); the 12-hour maximum and 15-minute lock still apply.

## 6. Upgrade

1. As Admin in the till: **Back up now** (or `.\SwiftCheckoutPOS.UI.exe --backup` in the program folder) and
   `.\SwiftCheckoutPOS.UI.exe --restore-drill` (PR-Q84: before every upgrade).
2. In the new package folder, Administrator PowerShell: `powershell -ExecutionPolicy Bypass -File .\install.ps1`
   (stops the service, copies the new program; data and settings are untouched).
3. `cd "C:\Program Files\SwiftCheckoutPOS"; .\SwiftCheckoutPOS.UI.exe --migrate`
4. `Start-Service SwiftCheckoutPOS`, sign in, check a receipt reprint and the Backup page.

If the new version fails: reinstall the previous package with `install.ps1`; if `--migrate` had already changed the database,
restore the pre-upgrade backup (`--restore <set name>`, DATA_RECOVERY_PLAN.md section 6).

## 7. Owner maintenance commands (program folder, Administrator prompt)

| Command | Purpose |
| --- | --- |
| `--migrate` | Apply database updates (after a backup) |
| `--backup` | Verified PreUpgrade backup |
| `--restore-drill [set]` | Monthly test restore into a separate database |
| `--restore <set>` | Replace live data from a backup (stop the service first) |
| `--reset-admin` | Set a new password for an Admin who is locked out |

## 8. Remove

`powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall` removes the service only. Program files and
`C:\ProgramData\SwiftCheckoutPOS` are kept; delete them by hand only after copying the backups away.
