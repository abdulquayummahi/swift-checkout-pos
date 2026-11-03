# Installation Checklist (shop PC)

Print this page and tick each line. Details: [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md).

## Before
- [ ] Windows 10/11 64-bit updated; Administrator account; date, time and time zone (Dhaka, UTC+6) correct
- [ ] SQL Server 2022 Express installed, instance `SQLEXPRESS`, Windows authentication, service set to Automatic
- [ ] Install package copied from USB (`SwiftCheckoutPOS-<date>` folder)
- [ ] A USB drive for daily backup copies, labelled, stored away from the PC

## Install
- [ ] `install.ps1` ran without red errors
- [ ] `SwiftCheckoutPOS.UI.exe --migrate` printed "Done."
- [ ] `sql-permissions.sql` printed "Permissions granted"
- [ ] `Start-Service SwiftCheckoutPOS`; `services.msc` shows it Running, Automatic (Delayed Start)
- [ ] Desktop shortcut opens the first-Admin setup page

## First setup
- [ ] First Admin created (password 12+ characters with letters and numbers, written down and locked away)
- [ ] Settings: receipt header filled; Manager financial view left OFF (unless the owner wants it)
- [ ] Users created for each Manager and Cashier (no shared logins)
- [ ] Categories, vendors and products entered; a few product images uploaded
- [ ] Backup: folder shown, automatic ON at 23:00, USB drive letter saved, **Back up now** shows Verified
- [ ] **Copy newest** to the USB drive succeeded
- [ ] `SwiftCheckoutPOS.UI.exe --restore-drill` printed "Passed"

## Function test (OFFLINE_TEST_REPORT "Shop PC" rows)
- [ ] Network cable unplugged / Wi-Fi off: till still opens and sells (OAT-02)
- [ ] From another PC on the network, `http://<this-pc>:5080` does **not** open (OAT-04)
- [ ] Open shift, sell with Cash, Card, bKash, Nagad, a split, and a Due sale with approval; close shift blind (OAT-09/35/18)
- [ ] Hold an order, restart the PC, resume it (OAT-05/13)
- [ ] Refund one item, void a sale within 5 minutes (OAT-15/36)
- [ ] Reprint the last receipt: shows REPRINT / COPY (OAT-21)
- [ ] Printer, scanner and drawer tested once chosen; `Printer:Mode` set in appsettings.Local.json (OAT-21/22)
- [ ] Sign-in takes about 1 second or more on this PC (PR-Q62); if much faster, tell the developer
- [ ] Leave the PC on past 23:00 once (or off overnight) and check a Daily backup appears next morning (OAT-25)
- [ ] Timed restore of a backup on a test run: selling again within 2 hours (OAT-27, PR-Q54)

Signed (owner): ______________________  Date: ____________
