# SwiftCheckoutPOS

Offline point-of-sale for one shop PC (Bangladesh, BDT). ASP.NET Core MVC on .NET 10, EF Core 10, SQL Server Express.
Runs as a Windows service and is used in a browser on the same PC at `http://localhost:5080`.

## Main features
Checkout with barcode scanner, held orders with stock reservation, Cash / Card / bKash / Nagad / Due (up to two tenders),
cash rounding to whole taka, role-limited discounts with Manager/Admin approval, refunds and voids, till shifts with blind close,
inventory with weighted average cost, finance ledger and reports, Admin-controlled financial privacy, receipts (virtual or ESC/POS)
with audited reprints, audit log, automatic verified backups with USB copy and restore drill.

## Repository
| Path | What |
| --- | --- |
| `SwiftCheckoutPOS/SwiftCheckoutPOS.UI` | Web app (controllers, views, Program.cs, maintenance commands) |
| `SwiftCheckoutPOS/SwiftCheckoutPOS.BLL` | Business rules and services |
| `SwiftCheckoutPOS/SwiftCheckoutPOS.DAL` | EF Core model, migrations, repositories |
| `SwiftCheckoutPOS/Tests` | Unit console checks and xUnit integration tests (guarded `SwiftCheckoutPOS_Test` database) |
| `SwiftCheckoutPOS/deploy` | `build-package.ps1`, `install.ps1`, `sql-permissions.sql` |
| `SwiftCheckoutPOS/docs` | Requirements, decisions, guides and reports |
| `AGENTS.md` | Rules for coding agents (test database only, no secrets) |

## Documents
* Owner rules: [OWNER_DECISIONS.md](SwiftCheckoutPOS/docs/OWNER_DECISIONS.md) · requirements: [PRODUCT_REQUIREMENTS.md](SwiftCheckoutPOS/docs/PRODUCT_REQUIREMENTS.md) · plan: [DEVELOPMENT_ROADMAP.md](SwiftCheckoutPOS/docs/DEVELOPMENT_ROADMAP.md)
* Install: [DEPLOYMENT_GUIDE.md](SwiftCheckoutPOS/docs/DEPLOYMENT_GUIDE.md) · [INSTALLATION_CHECKLIST.md](SwiftCheckoutPOS/docs/INSTALLATION_CHECKLIST.md) · [UPGRADE_GUIDE.md](SwiftCheckoutPOS/docs/UPGRADE_GUIDE.md)
* Backup and recovery: [DATA_RECOVERY_PLAN.md](SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md)
* Status: [PRODUCTION_READINESS.md](SwiftCheckoutPOS/docs/PRODUCTION_READINESS.md) · [OFFLINE_TEST_REPORT.md](SwiftCheckoutPOS/docs/OFFLINE_TEST_REPORT.md) · [FEATURE_GAP_REPORT.md](SwiftCheckoutPOS/docs/FEATURE_GAP_REPORT.md) · tests: [TESTING.md](SwiftCheckoutPOS/docs/TESTING.md)

## Developer quick start
1. .NET 10 SDK, SQL Server LocalDB or Express.
2. Connection string in user secrets: `dotnet user-secrets set ConnectionStrings:DbConn "<your dev connection>" --project SwiftCheckoutPOS/SwiftCheckoutPOS.UI`
3. `dotnet run --project SwiftCheckoutPOS/SwiftCheckoutPOS.UI -- --migrate`, then `dotnet run --project SwiftCheckoutPOS/SwiftCheckoutPOS.UI`
4. Tests: set the user environment variable `SWIFTCHECKOUTPOS_TEST_CONNECTION` to a connection for the database
   `SwiftCheckoutPOS_Test` only, then `dotnet test SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration` and run the unit
   console project. See TESTING.md.

## Branches (November 2026)
Work is on stacked branches, each built on the previous one (skip any already merged into `main`):
`BP-10-test-foundation` → `IMM-03-migration-baseline` → `IMM-04-retry-safe-transactions` → `IMM-05-money-rules` →
`BP-24-IMM-06-inventory-integrity` → `BP-03-held-reservations` → `BP-16-17-20-checkout-rules` → `BP-04-18-refunds-void` →
`BP-19-shifts-till` → `T14-BP-15-finance-privacy` → `BP-21-22-13-security` → `BP-23-08-receipts-printer` →
`T08-T10-backup-hosting` → `BP-07-security-hardening` → `BP-11-bounded-queries` → `STEP5-8-release-docs`.
Because each branch contains the ones before it, merging `STEP5-8-release-docs` into `main` brings in everything.
