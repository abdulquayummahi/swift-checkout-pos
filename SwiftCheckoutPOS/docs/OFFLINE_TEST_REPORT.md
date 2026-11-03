# Offline Test Report (playbook Step 5)

Date: 2026-11-04 · Code: branch chain ending in `STEP5-8-release-docs` · Machine: developer PC, LocalDB `SwiftCheckoutPOS_Test`.
Cases: [OFFLINE_ACCEPTANCE_TESTS.md](OFFLINE_ACCEPTANCE_TESTS.md) OAT-01…38.

**Result of automated runs:** 39/39 unit checks, 146/146 integration tests, 0 failures, 0 skipped
(`dotnet <Tests.Unit.dll>`; `dotnet test Tests/SwiftCheckoutPOS.Tests.Integration`). The integration suite includes an
in-memory **Production** host (`Host/HostSmokeTests.cs`) that signs in, renders every main page and makes a backup through the UI.

**Not yet run:** anything that needs the shop PC, its SQL Server service account, a real printer/scanner/drawer, a power cut or a
browser with the network unplugged. Those rows say **Shop PC** and must be done with [INSTALLATION_CHECKLIST.md](INSTALLATION_CHECKLIST.md).

Status key: **Pass (auto)** = covered by passing automated tests · **Partly** = code and some tests done, a manual part remains ·
**Shop PC** = manual test still to do on the installed PC · **Blocked** = waiting for an owner decision or hardware.

| OAT | Area | Status | Evidence / what remains |
| --- | --- | --- | --- |
| 01 | Test-target guard and reset | Pass (auto) | Guards/ConnectionGuardTests, MarkerGuardTests |
| 02 | Offline install, cold boot | Partly | No external src/href on any page (HostSmokeTests), CSP self-only. Shop PC: install from USB with network unplugged |
| 03 | Machine configuration | Partly | DataPaths + appsettings.Local.json; missing DbConn exits with message. Shop PC: real SQL instance |
| 04 | Loopback / HTTPS | Partly | AllowedHosts rejects other hosts (HostSmokeTests); Kestrel ListenLocalhost. Shop PC: check another PC cannot connect |
| 05 | Keys, login, durable work across restart | Partly | Persisted DPAPI keys; held orders in SQL (CQ-04/PR-Q17/18/CQ-20 tests). Shop PC: restart service mid-shift |
| 06 | Anonymous, roles, direct URLs | Pass (auto) | Unit: approved anonymous actions, Admin-only areas; host: anonymous redirects to login |
| 07 | Revocation, hashing, throttle, bootstrap | Pass (auto) | SecurityTests (CQ-09 ladder, IMM-09 stamp, PR-Q52); host one-time Setup. Shop PC: login time ≥ 1 s rule (PR-Q62) |
| 08 | Antiforgery | Pass (auto) | Global AutoValidateAntiforgeryToken; unit: no bypass; host POSTs carry tokens; QuickAdd fixed |
| 09 | Money precision and examples | Pass (auto) | PR-Q06/07/08/91, CQ-01 checkout tests |
| 10 | Sale atomicity / retry | Pass (auto) | SaveFailureRollsBack…, InvalidCommandHasNoSqlEffects, retry strategy |
| 11 | Double submit, two tabs, unknown commit | Pass (auto) | DuplicateCompletion…, ReceiptCannotBeReused…, ConcurrentCompletions… |
| 12 | Stock validation, stale forms | Pass (auto) | RevalidatesFreshProduct…, StaleEditFormIsRejected… |
| 13 | Hold / resume / cancel / expiry | Pass (auto) | CQ-04, PR-Q17, PR-Q18, CQ-20 tests. Shop PC: crash during hold |
| 14 | Restock / vendor return / ledger | Pass (auto) | WAC, zero-cost restock, vendor credit, ledger rollback tests |
| 15 | Refunds | Pass (auto) | Refund window, partial/damaged, Due-first payout, repeat/concurrency tests |
| 16 | Ledger and manual input | Pass (auto) | PR-Q33 allowlist, Manager expenses-only |
| 17 | COGS, profit, dates | Pass (auto) | MetricsTests (cost at sale, refunds on their day, half-open days, CQ-14) |
| 18 | Till reconciliation, real actor | Pass (auto) | ShiftTests (one shift, expected cash, blind close > ৳1,000 approval, No sale) |
| 19 | Customers and loyalty | Partly | Due limit/collection, customer totals (CustomerListTests), no invented points. Loyalty earning rule not defined (see FEATURE_GAP_REPORT) |
| 20 | Media, paths, XSS, safe errors | Pass (auto) | ProductImageStore tests, download containment, SafeErrors unit check, CSP. Razor encodes output by default |
| 21 | Saved-sale printing and reprint | Partly | ReceiptTests (REPRINT/COPY, ESC/POS bytes to a fake port). **Blocked**: printer model not chosen (PR-Q60) |
| 22 | Scanner and drawer | Blocked | Scanner/drawer models not chosen (PR-Q75/76); drawer pulse code tested on fake port |
| 23 | Local logs | Partly | Rolling file logs in DataRoot\Logs; host test creates them. Shop PC: inspect a real day's log |
| 24 | Manual backup and permission failures | Partly | BackupTests + backup through the UI. Shop PC: SQL service account permission on the backup folder |
| 25 | Scheduler, catch-up, missing config | Pass (auto) | Schedule/catch-up rules test; settings validation. Shop PC: PC off at 23:00, check backup next morning |
| 26 | Verification, USB copy, retention | Pass (auto) | VERIFYONLY + SHA-256, USB copy checksum, 30+12 retention test |
| 27 | Restore and failed restore | Partly | Drill + in-place restore with safety backup, tamper refusal. Shop PC: timed restore (RTO 2 h) |
| 28 | Crash / power loss | Shop PC | Pull the plug during a sale; confirm sale either saved or absent, backup catch-up |
| 29 | Migration and upgrade | Partly | MigrationModelTests (model = migrations). Shop PC: first upgrade with UPGRADE_GUIDE |
| 30 | Bounded queries, capacity | Partly | SQL aggregates, paging. **Blocked**: PR-Q63 volume not given |
| 31 | Backup boundary under active work | Partly | SQL backup is online and consistent; images zip taken right after (documented limit) |
| 32 | Offline tender labels, local-only traffic | Pass (auto) | Tender tests (Cash/Card/bKash/Nagad/Due, unknown rejected); no external requests |
| 33 | Replacement Windows, least privilege | Shop PC | `deploy/sql-permissions.sql`, ACLs in `install.ps1`; rebuild test on a spare PC |
| 34 | Financial privacy OFF / ON | Pass (auto) | T14/BP-15 tests; host crawl as Admin |
| 35 | Two tenders, Due ceiling, collection | Pass (auto) | Split tender, Due approval/limit tests |
| 36 | Void within five minutes | Pass (auto) | VoidWithinFiveMinutesReversesEverything |
| 37 | Combined discounts and approval | Pass (auto) | PR-Q12/67/93 tests |
| 38 | Receipt settings and saved reprint | Pass (auto) | Receipt sequence PR-Q45/CQ-13, header, REPRINT/COPY |

Summary: 24 pass automatically, 11 partly (manual part on the shop PC), 2 shop-PC only, 1 blocked on hardware.
