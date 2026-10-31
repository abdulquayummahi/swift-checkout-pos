# Checkout restoration

2026-10-31 · Subsequent implementation authorized by the owner: "CheckoutService.cs is lost. You need to rewrite checkout from the business rules." This authorization superseded the earlier documentation-only restriction for this checkout work. No schema, migration, appsettings/deployed machine configuration or package-version change was made by this work. Program.cs DI wiring was updated. No real database/app host was run.

## Rules used and boundaries

- **Owner-confirmed PR-Q14:** reject insufficient stock and ask staff to reduce quantity. Checked during cart addition and again against fresh tracked products inside the database unit.
- **Owner-confirmed interim PR-Q32:** associate a loyalty customer, leave points unchanged until a formula is defined. Missing/unlinked customers cannot complete a loyalty sale.
- Reuse the existing CheckoutSessionDTO/CartItemDTO calculation formulas: decimal amounts, two-digit ToEven rounding at their existing stages, flat bill discount precedence/cap and proportional discounted tax added to subtotal. These are compatibility behavior, **not approval of an undocumented tax/VAT/rounding policy**.
- Preserve source hold/resume stock behavior: hold reserves; resume releases once. No new expiry/valuation/discount/refund policy. All recoverable orders remain visible; an undecided retention setting cannot hide reservations.
- Existing Cash/Card/Mobile Wallet remain labels; no external authorization/payment API was added. Existing drawer triggering for all tenders remains pending PR-Q47.
- Product inactive/missing or changed price/tax is rejected for explicit review; checkout never silently uses a stale price, invents a replacement product or trusts a posted total.
- COGS uses immutable completion-time Product.CostPrice × sold quantity, following the existing system-COGS intent, recorded atomically rather than calling FinanceService's independent SaveChanges.

## Implemented paths and evidence

| Label / scope | Path, lines, class/method and excerpt |
| --- | --- |
| VERIFIED by build/regressions — service restored | [CheckoutService.cs:12](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/CheckoutService.cs:12), CheckoutService: AddToCart/Scan, quantity/removal, discount, customer, hold/resume/cancel and CompleteSale. Entry: `public sealed class CheckoutService(ICheckoutRepository repository)`. |
| CONFIRMED by UI→BLL→DAL trace — atomic retry boundary | [CheckoutController.cs:156](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs:156), CheckoutController.Finalize → [CheckoutService.cs:218](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/CheckoutService.cs:218), CompleteSale → [CheckoutRepository.cs:51–65](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.DAL/Repositories/CheckoutRepository.cs:51), Execute: `CreateExecutionStrategy()`; fresh context, `BeginTransaction()`, one `SaveChanges()`, `Commit()` inside strategy. Actual SQL fault injection is not VERIFIED. |
| VERIFIED by regressions — shortage, price/tax, duplicates and customer guards | [CheckoutService.cs:280](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/CheckoutService.cs:280), BuildItems: `product.StockQuantity < line.Quantity`; [CheckoutService.cs:324](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/CheckoutService.cs:324), RequireSameSale compares stored actor/tender/customer/totals/items before replay success. |
| VERIFIED by regressions — non-destructive recovery | [CheckoutService.cs:171](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/CheckoutService.cs:171), ResumeOrder: `sale.OrderStatus = Resumed`. Row/items retained; original RowVersion returned. Resume validation precedes release; repeat resume does not restore twice. |
| VERIFIED by regressions — safe print ordering and stale forms | [CheckoutController.cs:156–227](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs:156), Finalize/PrintCommittedSale: `CompleteSale` precedes `PrintCommittedSale`; deferred printer factory cannot fail controller construction; stale invoice/revision blocked. Unknown outcome keeps same receipt/tender locked. No automatic print/drawer replay. |
| VERIFIED by regressions — local display/virtual receipt precision | [VirtualPrinterService.cs:91](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs:91), BuildReceiptString uses saved receipt date/cashier/tender and N2 amounts. [VirtualPrinterService.cs:151](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs:151), PadColumns wraps oversized amounts rather than truncating them. |
| CONFIRMED — prerequisite wiring and state guards | [Program.cs:23](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.UI/Program.cs:23): `AddDistributedMemoryCache`; scoped ICheckoutRepository and deferred printer factory. [RefundService.cs:58–59](C:/source/repos/SwiftCheckoutPOS/SwiftCheckoutPOS.BLL/Services/RefundService.cs:58), ExecuteRefund: `sale.OrderStatus != "Completed"` rejects technical draft states. Existing refund transaction defect is otherwise outside this change. |

CheckoutViewModel now exposes HeldOrderDTO, not Sale entities. UI uses raw-price checkout product DTOs rather than InventoryService's whole-rounded product mappings. The unpaid direct-print route no longer actuates hardware; payment submission is saved first. Loyalty association is antiforgery-protected POST. Browser totals are never sent as payment authority.

### Persisted draft compatibility

Existing Sales/SaleItems columns are used without a schema change:

- Held: reserved snapshot.
- Resumed: reservation released once; saved snapshot remains recoverable if Session save fails.
- Completed: original sale ID/receipt retained, final facts and COGS saved together.
- Cancelled: clearing a current resumed draft retains its record; its reservation was already released. Clearing a stale local copy does not mutate a newer held/financial row.

New states fit the existing model's string column; actual database custom constraints remain uninspected. Completed sale/items are not rewritten. Legacy held rows lack per-line tax and original discount intent; resume reconstructs the flat discount amount and validates every stored line and aggregate. If current tax data cannot reproduce the stored totals, resume refuses before releasing stock and preserves the row. A percentage discount becomes its saved flat amount on reconstruction; original percentage intent cannot be recovered from the supplied schema.

## Regression suite and exact validation commands

Project: [Tests/SwiftCheckoutPOS.Tests.Unit](C:/source/repos/SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj), added to existing SwiftCheckoutPOS.slnx. It is a **dependency-free executable regression runner**, with no new test-framework package. Use the explicit commands below; `dotnet test` does not discover this runner. The two-project/framework-based SQL integration target in ARCHITECTURE.md remains future work.

**VERIFIED:** 35/35 checks passed. Coverage includes decimal preservation, shortage/stale price/tax, duplicate/mismatched receipt replay, fake lost acknowledgement/rollback, immutable cost/atomic staging, unchanged loyalty points, hold/rehold/resume/cancel, stale rows, Session failure, same-order pending lock, old-tab revision, printer factory/printing failure after commit, and long receipt values.

These are BLL/controller tests using a transactional **fake**, not proof of SQL Server retry/locking/rollback behavior. The test executable references UI types but never executes its Program/host, opens SQL or touches real devices. Virtual preview test creates no receipt files.

**Build:** source recompilation reported 23 existing warnings, 0 errors; the final incremental build after the checkout-view update reported 9 warnings, 0 errors. Warnings remain in unrelated DTOs, inventory/customer services/views and the serial-printer stub; no new checkout C# warning. No unrelated warning cleanup was attempted. Project/package files changed externally from 10.0.9 to 10.0.14 during this task; they were read again and left intact. Final build uses the then-current 10.0.14 references. JavaScript no longer parses/caps monetary discounts with floating-point numbers; the existing decimal cap runs on the server.

### Initial solution build

```powershell
dotnet build SwiftCheckoutPOS.slnx --no-restore
```

Exit 1 with no diagnostics (0 warnings/0 errors reported); did not establish compilation success.

### Single-node diagnostic build

```powershell
dotnet build SwiftCheckoutPOS.slnx --no-restore --disable-build-servers -m:1 -v:normal
```

Exit 0, 28 warnings/0 errors at this intermediate stage. Single-node/build-server-disabled invocation avoided the earlier silent failure; cause not established.

### Initial test restore

```powershell
dotnet restore Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj --source C:/Users/abdul/.nuget/packages --disable-parallel -p:NuGetAudit=false -v:minimal
```

Exit 1 after 'Determining projects to restore'; no dependency error detail. Retried single-node.

### Offline cached test restore

```powershell
dotnet restore Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj --source C:/Users/abdul/.nuget/packages --disable-parallel -p:NuGetAudit=false -m:1 -v:minimal
```

Exit 0; restored the test project and existing project references from local cache; no new direct package reference or package-version edit by this work.

### Initial regression build

```powershell
dotnet build Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj --no-restore --disable-build-servers -m:1 -v:minimal
```

Exit 0, 23 existing warnings/0 errors. No new-checkout C# warnings.

### Initial 27-check execution

```powershell
dotnet run --project Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj --no-build --no-restore
```

Exit 0, 27/27 passed before additional edge-case coverage. A later invocation of the same command stalled without output and was interrupted; direct assembly execution below succeeded.

### Final solution build

```powershell
dotnet build SwiftCheckoutPOS.slnx --no-restore --disable-build-servers -m:1 -v:minimal
```

Exit 0; source recompilation: 23 existing warnings/0 errors. Repeated after the final view-only edit: 9 incremental warnings/0 errors. Includes Razor and the added unit regression project.

### Final regression execution

```powershell
dotnet Tests/SwiftCheckoutPOS.Tests.Unit/bin/Debug/net10.0/SwiftCheckoutPOS.Tests.Unit.dll
```

Exit 0, 35/35 passed. Fakes only; no app host, SQL connection or physical device.

### Source evidence lookup

```powershell
rg -n 'public (sealed class CheckoutService|void HoldOrder|CheckoutSessionDTO ResumeOrder|void CancelCart|CompletedCheckoutDTO CompleteSale)|private static (List<SaleItem> BuildItems|void RequireSameSale|CheckoutSessionDTO RestoreCart)|public T Execute|CreateExecutionStrategy|BeginTransaction|SaveChanges|transaction.Commit|public IActionResult (Finalize|LookupLoyaltyCustomer|ResumeOrder)|private void (PrintCommittedSale|LogFailure)|EnsureEditable|AddDistributedMemoryCache|ICheckoutRepository|Func<IReceiptPrinter>|Only completed|ReceiptSaleDate|right.Length|cartVersion|Saving sale' SwiftCheckoutPOS.BLL/Services/CheckoutService.cs SwiftCheckoutPOS.DAL/Repositories/CheckoutRepository.cs SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs SwiftCheckoutPOS.UI/Program.cs SwiftCheckoutPOS.UI/Views/Checkout/Index.cshtml SwiftCheckoutPOS.BLL/Services/RefundService.cs SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs
```

Exit 0; located transaction/validation/printing paths cited below.

## Remaining release gates and uninspected areas

The original audit remains historical evidence; addressed code paths above do not mark its entire roadmap complete.

- **CONFIRMED existing AUD-03:** supplied migration/model drift remains; no deployed schema was read or changed. Current checkout expects existing model RowVersion/CostAtSale/ledger columns. A reviewed, backed-up schema reconciliation is still required before production.
- **SUSPECTED runtime:** real SQL transaction/unique-key/concurrency/retry/unknown-commit behavior needs dedicated SwiftCheckoutPOS_Test integration tests. No business database, SQL instance metadata, ACLs or records were inspected. Do not infer real SQL success from a fake's rollback.
- Receipt uniqueness plus payload comparison provides a compatible replay guard; this is not the full durable OperationId/fingerprint registry in the target architecture. The existing receipt format remains unchanged pending PR-Q45. A rare identical-payload receipt collision cannot be distinguished by current schema.
- Fresh active cart edits/pending UI state still use process Session; the held/resumed snapshot survives, but every later unsaved edit is not yet durably checkpointed. Full CheckoutOrder/Line persistence and durable outcome registry remain BP-03/IMM-04 follow-up work, not claimed complete.
- Real physical printing/drawer/scanner behavior, saved-history reprint and production boot/configuration/key/logging/offline acceptance were not tested. Printing failure cannot undo the saved sale; ambiguous delivery is not automatically repeated. All OAT cases remain unexecuted.
- Tax/rounding/discount/receipt/cancel/expiry/role/financial reporting owner questions remain open except the two answers above. Existing refund/finance/reporting/security/backup defects outside checkout were not re-audited or repaired. In particular legacy SalesRepository manual transactions remain for the separate refund workflow.

No migrations were applied, no database created/reset/restored, no business records modified, no appsettings/connection values printed or edited, and no package version selected/changed by this work. Recovery procedures remain exclusively in [DATA_RECOVERY_PLAN.md](C:/source/repos/SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md).
