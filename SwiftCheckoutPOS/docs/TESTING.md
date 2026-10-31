# SwiftCheckoutPOS testing — BP-10 / T01

Verified2026-10-31 (Asia/Dhaka). Working path: **C:/source/repos/SwiftCheckoutPOS**; Git root **C:/source/repos**. Windows10.0.26300, PowerShell Core7.6.5, .NET SDK10.0.403/runtime10.0.14. The owner's local copy may differ.

This is a **test foundation**, not a production-readiness sign-off. No production C#, migration, appsettings, production package or existing Unit source changed. [Owner decisions](OWNER_DECISIONS.md) remain authoritative. [Current-rule differences](PRODUCT_REQUIREMENTS.md#where-current-code-differs-from-approved-rules), [architecture](ARCHITECTURE.md#12-test-strategy-and-isolation-contract) and [roadmap](DEVELOPMENT_ROADMAP.md) remain linked.

## Running the two suites

Run from the Git root. Development test restoration may use internet/nuget.org; the **shipped POS app** retains its offline requirement. There is no offline staging/configuration and no new global.json.

```powershell
dotnet restore SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --source https://api.nuget.org/v3/index.json
dotnet build SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --configuration Debug --no-restore --no-incremental
dotnet SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/bin/Debug/net10.0/SwiftCheckoutPOS.Tests.Unit.dll
dotnet test SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration/SwiftCheckoutPOS.Tests.Integration.csproj --configuration Debug --no-build --no-restore --logger "console;verbosity=normal"
```

The existing Unit project remains an executable with **35 checks**, no test framework and unchanged source/project. dotnet test does not discover it. Keeping it avoids mixing migration of a working harness into T01. Any later conversion must preserve the check-name inventory and one-for-one coverage.

Integration targets net10.0, references BLL/DAL and runs under xUnit v3 + VSTest. It never starts the MVC host, Program.cs, AutoBackupWorker or real devices. Controller/middleware authorization is not exercised by these SQL tests.

| Integration-only direct package | Version |
| --- | --- |
| xunit.v3.mtp-off | 4.0.2 |
| xunit.runner.visualstudio | 4.0.1 |
| Microsoft.NET.Test.Sdk | 18.11.0 |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.14 |
| Microsoft.Data.SqlClient | 6.1.7 |

The first candidate xunit.v3.mtp-v2 4.0.2 restored/built but dotnet test failed **before execution**: its MTP2.5.1 targets reject the VSTest target on .NET10. It was replaced with the official **mtp-off** variant, preserving VSTest and avoiding repository-wide runner configuration. That final combination actually discovered and ran the suite. Production package references were not changed.

## Guarded developer database

The owner-approved simplified fixture reads **SWIFTCHECKOUTPOS_TEST_CONNECTION**, preferring the User setting and otherwise the process setting. It never reads UI DbConn, appsettings or User Secrets. Never print its value or put it on a command line.

- Before any connection: SqlConnectionStringBuilder must accept a local instance, exact case-sensitive catalog **SwiftCheckoutPOS_Test**, and no attach-file/failover. Invalid configuration produces a constant redacted error.
- The only master connection is on that same validated local instance, to check/create the fixed test-catalog name. No business catalog is opened; **SwiftCheckoutPOSDb is forbidden**.
- Existing catalog without the correct **dbo.BP10_TestFixture** marker stops, even if empty. Never add a marker just to adopt an unknown database.
- A new catalog gets owner key **SwiftCheckoutPOS.Tests.Integration.BP10.v1**, reviewed schema hash and Ready flag. Existing marker owner/hash/readiness must match. An incomplete setup stops for review.
- A session-level SQL application lock excludes a second fixture run. Every reset rechecks database identity/marker/schema, then deletes synthetic rows in FK order within one transaction; it preserves the marker/schema. No database/file drop or file-path manifest is used.
- All EF application contexts retain **EnableRetryOnFailure**. No separate database user/privilege impersonation is installed, per owner approval.
- The test database is retained between runs. Fixture data is synthetic and is deliberately cleared before each SQL characterization. Application database data is never backed up, cloned or reset.
- An actual SQL test temporarily changes the fixture marker, confirms reset refusal and unchanged synthetic rows, then restores that marker in finally.

The configured LocalDB connection was parsed without disclosure; integrated authentication, exact test catalog and absence of attach/failover were verified. SQL execution then succeeded only through this fixture. The standalone raw connection is never logged.

## Test schema and SQL review

Source: [CurrentModelBaseline.sql](../Tests/SwiftCheckoutPOS.Tests.Integration/Schema/CurrentModelBaseline.sql). SHA256:
`0C13442F28C715EFD0079F0C0B2490A9D7319D6D397496D6DF8B224C38CAA7B5`.

It was generated through EF10.0.14 **without opening SQL**, then read/reviewed before the first SQL fixture run:
- Creates10 current-model application tables and15 indexes, including unique barcode/receipt/username indexes.
- Preserves decimal(18,2) money, decimal(5,2) rate, product/sale SQL rowversions, required FKs and current NO ACTION delete behavior.
- Includes LedgerEntries/SystemSettings, Customer.Address, Sale.CashierName and SaleItem.CostAtSale missing from the migration chain.
- Contains no DROP/ALTER DATABASE/USE/BACKUP/RESTORE, real data, default credentials, production migration or migration-history stamping.
- The fixed-name test CREATE DATABASE and marker DDL are separate fixture operations. The SQL baseline is applied only to a newly created guarded test catalog.
- Before SQL access, the fixture compares reviewed deployed SQL to current EF generation. A changed model/baseline/marker hash stops; it is not auto-applied as an upgrade.

**AUD-03 remains unresolved.** The schema-drift characterization explicitly proves that the committed snapshot omits current fields/tables. The test-only baseline allows current service/repository tests; it does not qualify production initialization or upgrades. Production migration repair remains IMM-03.

To regenerate after an authorized model change, build first, then explicitly run only the metadata test:

```powershell
$env:SWIFTCHECKOUTPOS_GENERATE_TEST_SCHEMA = '1'
try {
    dotnet test SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration/SwiftCheckoutPOS.Tests.Integration.csproj --configuration Debug --no-build --no-restore --filter "FullyQualifiedName~SchemaScriptTests"
} finally {
    Remove-Item Env:SWIFTCHECKOUTPOS_GENERATE_TEST_SCHEMA
}
git diff -- SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Integration/Schema/CurrentModelBaseline.sql
dotnet build SwiftCheckoutPOS/SwiftCheckoutPOS.slnx --configuration Debug --no-restore
```

Generation mode is **not** a SQL-review result. Review the generated changes before any SQL run. An existing catalog with an obsolete marker/hash requires separate, explicitly approved test-database maintenance; do not bypass the guard. No dotnet ef migration command was run in BP-10; installed EF CLI10.0.8 was left unchanged.

## Characterization policy — mandatory retirement

**Every Current_…DiffersFrom… test documents OLD behavior, including known defects. Passing means that behavior was reproduced; it never approves the behavior.**

**When a later roadmap task fixes the corresponding DR, the SAME task must delete or replace that characterization with a regression test of the approved rule, and explicitly say which test was deleted/replaced in its report.** Preserve source/rule traceability. Do not keep the defective expectation as a permanent contract or silently rewrite expected values without reporting the change. The same principle applies to the AUD-03/PB-only characterizations.

The direct BLL DTO test with simultaneous discounts does not claim the UI setter permits both. The inventory read test proves projection alone does not mutate SQL; write/round-trip tests separately prove data loss. The refund repeat test exercises the BLL guard; no direct-repository weakness is misreported as a BLL-permitted duplicate.

| Current characterization method | Difference / audit | Profile and observed old behavior |
| --- | --- | --- |
| [Current_AddsVatOnTop_DiffersFromPRQ08](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line45 | DR-04 | Synthetic115 at15% becomes132.25; VAT is added. |
| [Current_TaxHalfPaisaUsesToEven_DiffersFromPRQ06](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line58 | DR-03 | Tax0.005 rounds to0.00, not0.01. |
| [Current_CashKeepsPaisa_DiffersFromPRQ07](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line70 | DR-05 | Cash10.49 stays10.49; no rounding field. |
| [Current_ServiceInputWithBothBillModesUsesFlat_DiffersFromPRQ12](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line82 | DR-06 | Direct BLL DTO with both modes prioritizes flat; UI setter is already exclusive. |
| [Current_HundredPercentDiscountNeedsNoApproval_DiffersFromPRQ67_PRQ93](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line97 | DR-07, DR-08 | 100% discount and below-cost net accepted without approval. |
| [Current_ReceiptExistsBeforeCompletionAndIsRetained_DiffersFromPRQ45](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line111 | DR-26 | Cart-created INV receipt persists at completion. |
| [Current_RejectsApprovedTenderVocabulary_DiffersFromPRQ85_PRQ86](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line140 | DR-27, DR-28 | bKash/Nagad/Due rejected. The fabricated Cash+Card label is tested separately as a correct invalid-input baseline. |
| [Current_ResumeReleasesStockThenCompletesOriginalSale_DiffersFromCQ04](../Tests/SwiftCheckoutPOS.Tests.Integration/Checkout/CheckoutCharacterizationTests.cs), line275 | DR-33 | Resume releases stock; completion reuses original sale and consumes stock. |
| [Current_ListAndEditWholeRoundMoneyAndRates_DiffersFromPRQ02_PRQ06](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line20 | DR-02 | List/edit whole-round prices and rates; SQL unchanged by read alone. |
| [Current_MetadataEditRoundTripLosesPaisa_DiffersFromPRQ02](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line44 | DR-02 | Renaming via edit DTO persists previously rounded prices/rate. |
| [Current_CreateRoundsMoneyBeforeRecordingInitialCost_DiffersFromPRQ02](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line61 | DR-02 | Create rounds cost/price/rate; initial cost uses rounded cost. |
| [Current_RestockWacUsesToEven_DiffersFromPRQ06](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line77 | DR-03 | WAC1.005 becomes1.00. |
| [Current_HeldStockIsExcludedFromWac_DiffersFromPRQ16](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line90 | DR-09 | Held quantity is omitted from owned-stock WAC basis. |
| [Current_LedgerFailureLeavesRoundedProductCommitted_DiffersFromAtomicInventory](../Tests/SwiftCheckoutPOS.Tests.Integration/Inventory/InventoryMappingCharacterizationTests.cs), line139 | DR-02, PB-03 | Rounded product/stock remains committed after ledger failure; PB-03 is the atomicity finding. |
| [Current_UnwrappedRefundFailsWithRetries_DiffersFromAtomicRefund](../Tests/SwiftCheckoutPOS.Tests.Integration/Refund/LegacyRefundCharacterizationTests.cs), line46 | DR-15, DR-34, PB-02 | Production-matching service/direct repository both fail with strategy conflict; no persisted refund. |
| [Current_RefundBodyAcceptsThirtyDayOldSale_DiffersFromPRQ27](../Tests/SwiftCheckoutPOS.Tests.Integration/Refund/LegacyRefundCharacterizationTests.cs), line93 | DR-14 | **Diagnostic caller, not current UI.** Diagnostic outer-strategy caller reaches body, which accepts30-day receipt. |
| [Current_RefundBodyRestocksEverythingWithoutCostOrPayoutEvent_DiffersFromPRQ65_PRQ66](../Tests/SwiftCheckoutPOS.Tests.Integration/Refund/LegacyRefundCharacterizationTests.cs), line106 | DR-15, DR-34 | **Diagnostic caller, not current UI.** Diagnostic body restores all stock, mutates status, leaves original COGS and has no refund event. |
| [Current_MigrationsOmitRequiredModelFields_DiffersFromAUD03](../Tests/SwiftCheckoutPOS.Tests.Integration/Schema/SchemaDriftCharacterizationTests.cs), line13 | AUD-03 | Snapshot still lacks fields/tables required by current model. |

Correct baseline tests have no fabricated DR mismatch. They cover real sale/items/stock/COGS commits, cost capture stability, validation failures, loyalty without point changes, sequential replay, payload conflicts and real-SQL rollback.

## Refund profiles and fault injection

The **unwrapped** service/direct-repository tests match production's retry configuration and verify the actual SqlServerRetryingExecutionStrategy conflict, with no persisted refund effects (PB-02). These passed by reproducing the failure, not by fixing it.

A separate **DiagnosticCallerStrategy_NotCurrentUI** profile supplies an outer execution strategy in test code, while all contexts still enable retries. This reaches the unchanged legacy refund body to characterize old-receipt acceptance, all-item restock, absent payout/cost events, sequential BLL repeat rejection and rollback. It is never inserted into production or substituted for the ordinary failure test.

FailAfterSave throws after real SQL SaveChanges while the repository's outer transaction is uncommitted; verification uses a fresh context after disposal/rollback. FailLedgerSave fails only the ledger stage and confirms the existing independent inventory commit (PB-03).

## Deterministic concurrency

Both requests use separate real contexts. DualSaveGate blocks the first two saves until **both** have completed reads and staged changes. No sleeps or timing guesses. Retries do not re-enter the barrier; a15-second missing-participant timeout fails with a diagnostic rather than silently continuing.

Two cases cover same receipt and different receipts competing for the last unit. The persisted invariant is one sale/item/COGS entry and stock0. A losing request may replay or report a known stock/concurrency conflict; winner order is not asserted.

Both cases passed in the complete suite and in **10 further repetitions each (20/20 passes,0 failed/0 skipped)**. No case needed Skip. If a future case cannot be made deterministic, mark Skip with the specific reason instead of accepting flaky failures.

## Executed verification

Commands above ran on2026-10-31 in the absolute working copy named at the top. Baseline before adding Integration: full solution build succeeded with27 existing warnings/0 errors, and35/35 Unit checks passed.

Final verification summary:
```text
Build succeeded.
    27 Warning(s)
    0 Error(s)

35/35 passed. No SQL connections, app host or physical devices used.

Test Run Successful.
Total tests: 82
     Passed: 82
     Failed: 0
    Skipped: 0

Controlled concurrency: 10 runs x 2 cases = 20 passed, 0 failed, 0 skipped.
```

Warnings are pre-existing production nullable/unused-field warnings, not new test warnings. Incremental Integration builds reported0 warnings/0 errors. Full rebuild and both suites must be rerun after relevant changes; earlier output is not a substitute.

Verified coverage includes82 discovered case instances (theories expand), SQL/marker guards, current-model/migration metadata, checkout/refund/inventory characterizations, rollback/partial commits, duplicate submissions and controlled concurrency. Existing35 checks remain unchanged. Discovery and actual SQL execution were proved; no claim is based on an empty dotnet test run.

## Remaining work and owner steps

- Current defects reproduced by passing characterizations remain production blockers; this task fixes none.
- No full MVC host/HTTP authorization/financial privacy, physical device, backup/restore, power-loss, unknown network commit, production migration or manual offline acceptance was qualified.
- PR-Q63 and CQ-21–23 remain open; no new rule was chosen.
- Developer prerequisites: .NET10 SDK, nuget.org access for restore, a working local SQL/LocalDB instance and the protected user environment variable. Do not copy business settings or backups.
- If configuration or marker checks fail, stop and inspect the **test** target; never change the business database or auto-adopt an existing catalog.
- There is **no POS deployment step or production migration** for BP-10. Only developers need these test files/packages.
