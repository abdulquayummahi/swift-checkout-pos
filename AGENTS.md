# SwiftCheckoutPOS — Agent Instructions

These instructions apply from the Git root, which contains this file and SwiftCheckoutPOS/.
State the absolute working path in every task report; the owner's copy may differ.
Current source is implementation evidence, not business-rule authority.
Rules below describe required behavior; several controls are still roadmap work.

## 1. Product context and authority

- Keep the existing C# ASP.NET Core MVC POS on one offline Windows computer.
- Projects target net10.0; UI uses Microsoft.NET.Sdk.Web; BLL/DAL use Microsoft.NET.Sdk.
- EF Core SQL Server/Design/Tools references are 10.0.14; System.IO.Ports is 10.0.14.
- Local cookie authentication, ASP.NET Core Session and local hardware remain in scope.
- Production target: SQL Server 2022 Express as a Windows service, per ARCHITECTURE.md §7.
- A compatible licensed existing local instance may remain; installed edition is unverified.
- Local hosting/assets/configuration/logging/backup/restore must survive restart and upgrades.
- No cloud, online identity, branches, network terminals, sync or distributed infrastructure.
- Do not replace MVC, SQL Server or the three-project architecture without a concrete reason.
- Read relevant docs under SwiftCheckoutPOS/docs/ before changing behavior:
  - OWNER_DECISIONS.md is the approved business-rule authority; PRODUCT_REQUIREMENTS.md merges it.
  - ARCHITECTURE.md defines technical direction and §12 database-test guards.
  - DEVELOPMENT_ROADMAP.md supplies task IDs, dependencies, decision gates and done-evidence.
  - DATA_RECOVERY_PLAN.md is the sole backup/restore/upgrade recovery procedure.
  - PROJECT_AUDIT.md is historical evidence; CHECKOUT_RESTORATION.md records restored behavior.
- Checkout was restored with pre-decision compatibility formulas and reservation behavior.
- Approved rules override that code; current differences are in PRODUCT_REQUIREMENTS.md §5.
- If a needed rule is missing, blank, PENDING, CHECK, contradictory or ambiguous: STOP and ask.
- Never infer tax, discount, refund, rounding, inventory or financial policy from current code.
- Keep open questions open until the owner approves an answer; do not invent a resolution.

## 2. Repository layout and commands

All paths/commands below are relative to the Git root, not the nested project folder.
- Solution: SwiftCheckoutPOS/SwiftCheckoutPOS.slnx; do not create a parallel .sln.
- UI: SwiftCheckoutPOS/SwiftCheckoutPOS.UI/SwiftCheckoutPOS.UI.csproj.
- BLL: SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/SwiftCheckoutPOS.BLL.csproj.
- DAL: SwiftCheckoutPOS/SwiftCheckoutPOS.DAL/SwiftCheckoutPOS.DAL.csproj.
- Context: SwiftCheckoutPOS/SwiftCheckoutPOS.DAL/Context/ApplicationDbContext.cs.
- Migrations: SwiftCheckoutPOS/SwiftCheckoutPOS.DAL/Migrations/.
- Unit runner: SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/Program.cs.
- Assets: SwiftCheckoutPOS/SwiftCheckoutPOS.UI/wwwroot/; Razor: SwiftCheckoutPOS/SwiftCheckoutPOS.UI/Views/.
- No global.json is present in this tree/checked ancestors; .NET 10 SDK is required.
- Last checked SDK: 10.0.403. Recheck locally; do not silently pin or install a new SDK/tool.
- Unit csproj has OutputType=Exe, net10.0 and a UI reference, with no test-framework package.

Normal restore/build and existing fake-only unit suite:
```powershell
dotnet restore SwiftCheckoutPOS/SwiftCheckoutPOS.slnx
dotnet build SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --configuration Debug --no-restore
dotnet SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/bin/Debug/net10.0/SwiftCheckoutPOS.Tests.Unit.dll
```
Build before running the DLL; dotnet test does not discover this console suite.
Do not replace the runner or add packages as an incidental cleanup.

Integration (implemented by BP-10 / T01; the absence statement in section9 is historical):
SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration/SwiftCheckoutPOS.Tests.Integration.csproj.
xUnit v3 mtp-off4.0.2 / VSTest adapter4.0.1 / Test SDK18.11.0; retain the separate Unit runner.
Restore development test packages normally from nuget.org; no offline staging is required.
The owner-approved LocalDB guard uses pre-connection validation and a DB marker, with no file manifest
or separate DB user. See SwiftCheckoutPOS/docs/TESTING.md and ARCHITECTURE.md §12 for that test-only exception.
Run against only the guarded, marked SwiftCheckoutPOS_Test (never the Development DbConn):
```powershell
dotnet restore SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --source https://api.nuget.org/v3/index.json
dotnet build SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --configuration Debug --no-restore --no-incremental
dotnet test SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration/SwiftCheckoutPOS.Tests.Integration.csproj --configuration Debug --no-build --no-restore
```
No UI host, background worker or physical hardware is started by this Integration suite.

EF command templates: run only in an explicitly authorized schema task, after target guards.
Supply nonempty approved $posMigrationName/$posFromMigration/$posToMigration/$posReviewedSqlPath.
The SQL output path is task-owned; never overwrite source, secrets or business recovery files.
Use a current build and verify CLI/provider compatibility; last local EF CLI was 10.0.8.
No design-time factory exists; the Integration fixture guard does not protect EF CLI host commands.
Until verified isolation exists, do not execute design-time host/migration commands.
```powershell
$posDalProject = 'SwiftCheckoutPOS/SwiftCheckoutPOS.DAL/SwiftCheckoutPOS.DAL.csproj'
$posUiProject = 'SwiftCheckoutPOS/SwiftCheckoutPOS.UI/SwiftCheckoutPOS.UI.csproj'
$env:DOTNET_ENVIRONMENT = 'Testing'
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
# Required protected input: parse/allowlist it before any connection; never echo its value.
$env:ConnectionStrings__DbConn = $env:SWIFTCHECKOUTPOS_TEST_CONNECTION
dotnet ef migrations add $posMigrationName --project $posDalProject --startup-project $posUiProject --context ApplicationDbContext --output-dir Migrations -- --environment Testing
dotnet ef migrations script $posFromMigration $posToMigration --project $posDalProject --startup-project $posUiProject --context ApplicationDbContext --output $posReviewedSqlPath -- --environment Testing
# Only when explicitly authorized, after SQL review/backup, and ONLY on SwiftCheckoutPOS_Test:
dotnet ef database update $posToMigration --project $posDalProject --startup-project $posUiProject --context ApplicationDbContext -- --environment Testing
```
Environment labels/variables alone are not a target guard; empty input must fail closed.
Never use default Development configuration for these commands or print connection arguments.

## 3. Architecture rules

- Actual references remain UI→BLL/DAL and BLL→DAL; DAL never references BLL/UI.
- UI owns HTTP binding, ModelState, authentication/authorization, ViewModels, Razor and composition.
- No DbContext, SQL, repository queries or transaction orchestration in controller actions.
- BLL owns use cases, authoritative validation, approved calculations/transitions and DTOs/results.
- DAL owns entities, mappings, migrations, narrow repositories and atomic database execution.
- Views/forms use DTOs/ViewModels only; never entities, navigations or entity wrappers.
- Do not expose IQueryable to UI; use bounded SQL projections/aggregation/paging.
- Keep BLL→DAL; introduce narrow DAL-owned interfaces only for a concrete test/transaction benefit.
- Do not introduce generic repositories, mediators, new layering projects or distributed patterns.
- Constructor injection; scoped services/repositories/context/printer; no I/O in constructors.
- Use a fresh context per retry attempt; never share a context across requests or threads.
- Hosted singleton AutoBackupWorker creates/disposes a scope per attempt; no captured scoped state.
- Keep time/options/local coordination singletons only when thread-safe, per architecture.
- UI rejects binding errors; BLL normalizes and enforces business rules even if UI validated.
- Revalidate fresh stock/status/price/customer/permissions inside the database operation.
- Return explicit validation/conflict/authorization/outcome results; show safe actionable messages.
- Central UI exception handling logs a sanitized correlation ID, never raw SQL/stack/ex.Message.
- Hardware stays behind SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Interfaces/IReceiptPrinter.cs and local adapters.
- Print/open drawer after commit; device or diagnostic failure never rolls back/repeats a sale.
- Treat unknown print delivery separately; no automatic duplicate drawer pulse or financial retry.

## 4. Money and inventory integrity

- decimal only for money/rates; never double/float. Use explicit BDT/paisa formatting.
- Every monetary SQL column has explicit precision; preserve existing values in reviewed migrations.
- One central BLL calculator; server recalculates totals from fresh trusted data, not posted totals.
- Use Math.Round(value, 2, MidpointRounding.AwayFromZero) at approved stages; digits0 only for cash.
- Prices/rates retain two decimals; round calculated lines, aggregate VAT and exact total to two decimals.
- Prices are VAT-inclusive; current unregistered shop has 0% VAT and no VAT/BIN receipt line.
- Cash remainder alone rounds to whole taka; Card/bKash/Nagad/Due stay exact (CQ-01/16).
- Save/print sale and refund cash rounding separately; never merge it into revenue/VAT.
- Cash rounding affects Net Profit/average profit/payback as other income/expense (CQ-19).
- Apply approved proportional-discount/residual/cost-floor and role-approval rules; read their IDs.
- Preserve immutable CostAtSale, VAT, discount, tender and receipt facts; never invent historic cost.
- All related sale/items/tenders/stock/ledger/reservation/approval/audit writes form ONE transaction.
- Retain EnableRetryOnFailure; start manual transactions inside CreateExecutionStrategy().Execute/ExecuteAsync.
- Recreate state/context each attempt; the DAL operation runner owns SaveChanges/commit.
- Write repositories stage changes, never independently commit half of a BLL use case.
- Sale/refund/void and other money/stock submissions are idempotent with durable operation identity.
- Verify an uncertain commit by the same identity; do not request a new sale after an unknown outcome.
- Protect stock/draft/settings changes with original row versions and database constraints.
- Check available stock transactionally; reject shortages, missing/inactive items and stale requests.
- Owned stock includes reservations in valuation/WAC; reserve availability, not business ownership.
- Resume retains reservation; completion consumes once, cancellation releases once.
- CQ-20: expire Held only; no expiry while Resumed/open; rehold restarts the configured clock.
- Never hard-delete completed sales or financial records; retain linked authorized reversals.
- Restrict destructive cascades; deactivate referenced master data instead of losing history.
- Every stock change records user/time/reason and its approved financial effect.
- Audit void/refund/discount override/price change/stock adjustment/drawer/settings actions atomically.
- Refunds preserve original sale facts, enforce cumulative quantities and use actual paid tender history.
- Open-shift/user attribution, due limits, refund/void windows and receipt numbering follow owner decisions.
- Do not silently resolve remaining questions in PRODUCT_REQUIREMENTS.md §7.

## 5. Financial privacy

- PR-Q50/92: financial information is Admin-only by default.
- Admin-only audited AllowManagerFinancialView=false may permit Manager financial READ.
- Enforce CanViewFinancials server-side before querying/projecting/serializing protected data.
- Use distinct operational/financial DTOs; hiding buttons/columns is never access control.
- No protected HTML/JSON/hidden fields/scripts/exports/sort keys/errors for unauthorized users.
- Protect cost/margin/value/COGS/profit/ledger/capital/loans, portfolio due and all period totals/counts.
- Manager ON does not grant user/system/backup powers or additional financial write authority.
- Backup/list/download/restore remain Admin-only even when Manager financial view is ON.
- Follow CQ-03/10/12/15 exceptions precisely: write-only Manager cost entry, served-customer due,
  own-today six-category expenses, one selected refund receipt and one-shift reconciliation.
- Cashier never receives expected cash/discrepancy, including after its blind count.
- Below-cost approval is Admin-only when financial view OFF; Manager/Admin when ON.
- Test every FP surface in the roadmap with Manager OFF/ON, Cashier and Admin.

## 6. Data safety

- Never run the app, migrations or tests against SwiftCheckoutPOSDb or any business database.
- SwiftCheckoutPOS/SwiftCheckoutPOS.UI/appsettings.Development.json targets business data; never inherit it.
- Automated database work uses ONLY SwiftCheckoutPOS_Test with ARCHITECTURE.md §12 guards.
- Guard exact local instance/catalog, fixture marker and file roots before opening/resetting anything.
- Only guarded provisioning may use master on that same allowlisted test instance to create the test DB.
- Existing unmarked/mismatched databases cause STOP; no suffix-only or environment-only safeguards.
- Review migration/model drift and generated from/to SQL for drops, cascades and precision/data loss.
- Verify a pre-change recovery set before applying a migration; never auto-Migrate/EnsureCreated at startup.
- Production upgrades/restores are controlled operator work under DATA_RECOVERY_PLAN.md, not agent DB access.
- Keep SQL data, machine config, keys, images, logs, backups and exports outside install/temp folders.
- Target protected ProgramData roots, machine-local DbConn, persisted Data Protection keys and local ACLs.
- Daily 23:00 Dhaka + catch-up backup; 30 daily/12 monthly, daily USB; RPO 1 business day/RTO 2h.
- Monthly/pre-upgrade restore drills and paired images/settings follow the sole recovery plan.
- Never delete business history to fit disk/database limits or use destructive startup repair.

## 7. Security

- Require authentication for every page/action except the login entry; authorize every action by role/policy.
- Validate actor/security version server-side; never trust posted user/approver IDs or stale cookie privileges.
- Preserve versioned PBKDF2-SHA256: current 210,000 iterations, 32-byte salt/key, constant-time verification.
- Preserve legacy 100,000-iteration verification; any work-factor/rehash change follows PR-Q62 and tests.
- No seeded/default real passwords; owner-only first Admin/recovery, Admin-authorized account reset.
- Target absolute login ≤12h, 15-minute inactivity password lock preserving work; no sliding extension.
- Current Program.cs still uses 8h; implement approved settings rather than treating this as compliance.
- CQ-09: per-username fifth failure 30s, then 60/120/240/300s cap; reset on success or 30min without failure.
- Secure HttpOnly/SameSite cookies under local HTTPS; session is not durable business storage or idle lock.
- Antiforgery on EVERY state-changing request; GET never changes records or actuates hardware.
- No Html.Raw/unencoded output/JS concatenation for user data; validate and encode by output context.
- No concatenated/interpolated SQL, including backup/restore commands; parameterize using the recovery plan.
- Validate uploads/content/size/generated names; canonicalize paths and contain file access/deletion.
- Bind Kestrel to loopback only; provision trusted local HTTPS and protect certificate/key access.
- Secrets/connection values never enter the repository, reports or logs; it may be public.
- Never log passwords, hashes, credentials, tokens, customer data or sensitive financial values.
- Use ILogger with redacted local rotated logs per architecture; safe errors, no production developer page.

## 8. Offline requirements

- No runtime CDN, external fonts/images, remote APIs, telemetry or online licensing.
- Bundle frontend libraries under SwiftCheckoutPOS/SwiftCheckoutPOS.UI/wwwroot/.
- Core login, sales, stock, receipts, reports and recovery must work without internet.
- Development NuGet restore is not a runtime dependency; ship required assets/runtime offline.
- Local Windows/app/SQL startup and persisted state must recover after reboot/crash/power loss.
- Keep virtual hardware explicitly labelled; a virtual receipt is not proof of physical printing.

## 9. Testing

- Existing Unit console suite uses fakes, starts no app/SQL/device; inspect changes before running it.
- Preserve characterization tests as evidence, not as authority for pre-decision business behavior.
- T01/BP-10 adds Integration to the existing solution; it does not yet exist.
- Integration runs only on guarded SwiftCheckoutPOS_Test with synthetic fixtures and isolated identities.
- No UI DbConn/User Secrets fallback, real backups/customer data, shop keys or physical devices in automation.
- Reset only the marked allowlisted test catalog/roots; guard before destructive actions, serialize resets.
- Use actual SQL Server for transaction/constraint/concurrency/backup tests; no EF InMemory/SQLite substitute.
- Cover checkout/sales, stock/WAC/holds, refunds/voids/due, money/VAT/profit and till/shift arithmetic.
- Cover retries/rollback/unknown commit, double submissions/two tabs, stale rows and failure injection.
- Cover hashing/auth/revocation/roles/antiforgery, financial privacy OFF/ON and safe files/errors.
- Cover backup/restore/scheduler/restart/upgrades, offline assets and postcommit hardware failure/reprint.
- OFFLINE_ACCEPTANCE_TESTS.md holds cases; record actual evidence, never mark an unexecuted case passed.

## 10. Workflow and reporting

- One task per Git branch named after its roadmap ID (for example BP-15-financial-privacy).
- Honor task scope; preserve unrelated owner changes; no incidental packages/config/schema/features.
- Before code changes, trace UI→BLL→DAL; establish root cause, affected files, DB changes and regression risks.
- Prepare a concrete plan; if asked to plan first, STOP and wait for approval before implementing.
- Do authorized work without adding redundant approval gates; missing business rules still require an answer.
- Update affected documentation in the same implementation task unless the user restricts file scope.
- After changes report absolute working path, files changed, exact commands/tests and actual results.
- Run relevant tests and a full solution build when implementation scope permits; report skipped checks.
- Include migration/upgrade notes, dependencies, unresolved issues and what was not verified.
- Never claim success/readiness from code inspection alone; separate current facts from target requirements.
- Findings cite file/line/class/method/excerpt and use VERIFIED / CONFIRMED / SUSPECTED per the audit.
- .gitignore contains Backup*/ and !**/Views/BackupAdmin/; new Backup-prefixed folders need an exception.
- Check git check-ignore -v and Git status/tracked files; do not silently force-add ignored source.
- If scope forbids .gitignore edits, report the needed exception rather than changing it.

## 11. Definition of done

- Approved requirements met; existing functionality and records preserved.
- Financial/inventory integrity, financial privacy and security maintained.
- Relevant tests pass and the solution builds with actual results recorded.
- Offline operation, restart/recovery and postcommit hardware isolation preserved.
- Required migration SQL/recovery evidence reviewed; no unauthorized business database access.
- Affected docs updated; unresolved/untested items explicitly stated; no unrelated changes.
