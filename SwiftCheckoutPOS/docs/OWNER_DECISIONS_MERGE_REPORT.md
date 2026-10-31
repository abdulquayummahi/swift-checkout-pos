# Owner decisions documentation merge report

2026-10-31 (Asia/Dhaka). **Documentation only.** Current OWNER_DECISIONS.md is business-rule authority; no source/test/migration/package/appsettings/AGENTS changes, app/build/test run, database connection or database operation by this merge. Earlier checkout regression/build results are historical evidence, not newly executed acceptance.

## 1. Files changed

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [CHECKOUT_RESTORATION.md](CHECKOUT_RESTORATION.md) — historical-baseline notice and relative links.
- [DATA_RECOVERY_PLAN.md](DATA_RECOVERY_PLAN.md)
- [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md)
- [EVIDENCE_REFERENCES.md](EVIDENCE_REFERENCES.md) — inert snapshot/provenance for two untracked link targets.
- [OFFLINE_ACCEPTANCE_TESTS.md](OFFLINE_ACCEPTANCE_TESTS.md)
- [OWNER_DECISIONS.md](OWNER_DECISIONS.md) — only one dated merge line under the title; answers untouched.
- [OWNER_DECISIONS_MERGE_REPORT.md](OWNER_DECISIONS_MERGE_REPORT.md)
- [PRODUCT_REQUIREMENTS.md](PRODUCT_REQUIREMENTS.md)
- [PROJECT_AUDIT.md](PROJECT_AUDIT.md) — links/line-reference formatting only; prose/findings untouched.

The two owner-run NuGet .txt reports were read and left unchanged. Initial missing/old-file observations were superseded after the owner saved the new version. No playbook file was created; the owner supplied T01–T15 titles and the crosswalk is in DEVELOPMENT_ROADMAP.md.

## 2. PR-Q status

**93 rows: 92 ANSWERED, 1 OPEN. OPEN IDs: PR-Q63 only.** Its capacity estimate is explicitly PENDING. PR-Q60/75/76 approve deliberate model deferral with specified virtual/HID/RJ11 mode; no model was invented. Cross-rule questions below do not erase approved answers or silently settle their interactions. The complete status/approved-answer/current-behavior/source table is [PRODUCT_REQUIREMENTS.md section 6](PRODUCT_REQUIREMENTS.md#6-owner-decision-register).

Approved merge highlights: currently unregistered 0% VAT/no VAT or BIN receipt line; inclusive extraction retained for future registration; explicit AwayFromZero; cash-only whole-taka rounding; combined discount caps25/50 with escalation; held ownership/expiry; seven-day approved refunds; explicit shifts;12h+15min lock; five-failure delay; two tender/Due/void; default-private financials and audited Admin toggle; daily23:00 Dhaka recovery/USB,30+12 retention,RPO one business day,RTO two hours,monthly/pre-upgrade drill.

NuGet declarations were read as10.0.14 for the listed Microsoft/System packages; Newtonsoft remains13.0.4. Owner reports supplied/file-dated2026-10-31 report no vulnerabilities or updates given their sources; execution timestamps/revision hashes are not embedded. IMM-11 /T11 is **Done — verify**, not a newly run network audit.

## 3. Code versus approved rule differences

**32 CONFIRMED code-path differences; no live runtime/data-loss reproduction.** The [canonical full issue table](PRODUCT_REQUIREMENTS.md#where-current-code-differs-from-approved-rules) includes severity, label, file/line/class/method/excerpt, root cause/impact, correction, migration need and regression risk. Current source includes the restored CheckoutService.CompleteSale→CheckoutRepository.Execute path, not only the legacy repository.

| ID · severity | PR-Q | Approved rule / current mismatch | Current source evidence |
| --- | --- | --- | --- |
| DR-01 · Medium | 01 | BDT independent of Windows culture — Host-culture currency can display a different currency. | [SwiftCheckoutPOS.UI/Views/Report/ZReport.cshtml](../SwiftCheckoutPOS.UI/Views/Report/ZReport.cshtml), lines 35–45 — ZReport Razor: `Model.GrossRevenue.ToString("C")` |
| DR-02 · Critical | 02–05,06 | Keep two-decimal prices/rates without destructive display/edit rounding — Whole-round mappings/writes lose paisa and fractional rates; restored checkout's raw-price path does not repair inventory editing. | [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 37–43,96–98,120–126,152 — InventoryService mappings/create/edit: `CostPrice = Math.Round(p.CostPrice)`; [SwiftCheckoutPOS.UI/Views/InventoryAdmin/Edit.cshtml](../SwiftCheckoutPOS.UI/Views/InventoryAdmin/Edit.cshtml), lines 69–93 — Edit Razor: `asp-format="{0:F0}"` |
| DR-03 · Critical | 05,06 | Midpoints round away from zero — Default ToEven differs at .005; DTO remains the restored checkout's calculation engine. | [SwiftCheckoutPOS.BLL/DTOs/CartItemDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CartItemDTO.cs), lines 17 — CartItemDTO.LineTotal: `Math.Round(..., 2)`; [SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs), lines 33–68 — calculated totals: `return Math.Round(tax, 2);` |
| DR-04 · Critical | 08,10 | Inclusive prices; currently unregistered, all new products 0% VAT, no VAT/BIN receipt line, Admin controls future VAT — Code adds VAT, permits nonzero rates and always prints tax/BIN; at 0% the arithmetic difference is dormant, not proof of an observed overcharge. | [SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs), lines 59–68 — TaxAmount/TotalAmount: `NetSubTotal + TaxAmount`; [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 380–383 — ValidateProduct: `product.TaxRate < 0 \|\| product.TaxRate > 100`; [SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs](../SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs), lines 85–88,124 — BuildReceiptString: `PadColumns("TAX:", ... )` |
| DR-05 · Critical | 07,91 | Whole-taka rounding for cash only, exact Card/bKash/Nagad, saved rounding line — No cash-specific payable/delta field; a single exact total serves all tenders. | [SwiftCheckoutPOS.DAL/Entities/Sale.cs](../SwiftCheckoutPOS.DAL/Entities/Sale.cs), lines 19–32 — Sale fields: `public decimal TotalAmount { get; set; }`; [SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs), lines 68 — TotalAmount: `Math.Round(NetSubTotal + TaxAmount, 2)` |
| DR-06 · High | 12 | Reject commands containing simultaneous flat and percentage bill discounts — UI/service setter is exclusive, but the BLL accepts a pre-existing DTO containing both and silently prioritizes flat; no confirmed browser path sets both today. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 100–115,430–432 — ApplyBillDiscount/ValidateCart: `cart.BillDiscountPercentage = 0;`; [SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs), lines 40–41 — CalculatedDiscount: `if (BillDiscountAmount > 0)` |
| DR-07 · High | 13,67,90 | Item discounts plus combined per-role limits and recorded escalation approval — Only bill-discount UI/action; no item-discount command, role/combined-limit/approver facts. Cashier can request up to 100%. | [SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs](../SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs), lines 14,50–52 — Authorize/ApplyBillDiscount: `[Authorize]`; [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 100–115,427–432 — discount validation: `if (value > 100) Fail(...)` |
| DR-08 · Critical | 22,67,90,93 | Block below-cost net items unless Manager/Admin approves with reason; private error messages — Checkout does not compare discounted net item value with cost; inventory hard-blocks even authorized overrides; no reason/approval workflow. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 280–297,427–432 — BuildItems/ValidateCart: `CostAtSale = product.CostPrice`; [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 112,150,177–178 — create/edit/restock guards: `Selling price cannot be lower than wholesale cost price.` |
| DR-09 · Critical | 16,19 | Held units remain in owned inventory value and WAC — Available stock doubles as owned quantity, so held units are omitted from WAC basis. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 157–158 — HoldOrder: `StockQuantity -= item.Quantity`; [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 168–172 — RestockProduct: `Math.Max(0, product.StockQuantity) * product.CostPrice` |
| DR-10 · High | 17,18,49 | Direct retained cancellation, 72h expiry (0=never), hold/resume actors — Resume releases once and retains SQL (improvement); cancellation only changes current Resumed copy, no expiry consumer and no separate resumed-by fact. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 171–210 — ResumeOrder/CancelCart: `sale.OrderStatus != Resumed`; [SwiftCheckoutPOS.DAL/Repositories/CheckoutRepository.cs](../SwiftCheckoutPOS.DAL/Repositories/CheckoutRepository.cs), lines 28–35 — GetRecoverableOrders: `s.OrderStatus == "Held" \|\| s.OrderStatus == "Resumed"`; [SwiftCheckoutPOS.BLL/Services/SettingsService.cs](../SwiftCheckoutPOS.BLL/Services/SettingsService.cs), lines 17–36 — retention setting: `GetSettingValue("HeldOrderRetentionHours", "72")` |
| DR-11 · High | 20 | Zero-cost restock only free/bonus with reason; no cash ledger entry — Zero accepted without reason and posted as an Inventory Outflow even at zero. | [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 160–186 — RestockProduct: `if (totalBatchCost < 0) ...`; [SwiftCheckoutPOS.BLL/Services/FinanceService.cs](../SwiftCheckoutPOS.BLL/Services/FinanceService.cs), lines 138–149 — RecordSystemInventoryCost: `Amount = totalCost` |
| DR-12 · Critical | 23,24,82 | Attributable stock adjustments with approved expense/gain at current average cost — Absolute edit changes quantity without reason, movement or financial effect; stale-form issue PB-04 remains. | [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 140–156 — UpdateProduct: `entity.StockQuantity = dto.StockQuantity;` |
| DR-13 · High | 25 | No-cash vendor return records pending credit and reduces value; post cash when received — Quantity is reduced and cash conditional is present, but no pending-credit/settlement record or note. | [SwiftCheckoutPOS.BLL/Services/InventoryService.cs](../SwiftCheckoutPOS.BLL/Services/InventoryService.cs), lines 190–203 — ReturnToVendor: `if (refundAmount > 0) ...` |
| DR-14 · High | 26,27,28 | Completed receipt within 7 days with approval, full v1/partial next, original tender — Completed/repeat guards exist, but no 7-day check, independent approval fact, refund tender/quantity state; partial deferred explicitly by owner. | [SwiftCheckoutPOS.UI/Controllers/RefundController.cs](../SwiftCheckoutPOS.UI/Controllers/RefundController.cs), lines 12,44–58 — ConfirmRefund: `_refundService.ExecuteRefund(receiptNumber);`; [SwiftCheckoutPOS.BLL/Services/RefundService.cs](../SwiftCheckoutPOS.BLL/Services/RefundService.cs), lines 48–62 — ExecuteRefund: `Only completed sales can be refunded.` |
| DR-15 · Critical | 29–31,65,66 | Refund event date; preserved original sale; disposition choice and linked VAT/COGS/money reversals — Original status mutated, all items restocked, no refund occurrence/actor/payout/VAT/cost event. Legacy retry defect PB-02 also remains. | [SwiftCheckoutPOS.DAL/Repositories/SalesRepository.cs](../SwiftCheckoutPOS.DAL/Repositories/SalesRepository.cs), lines 161–179 — ProcessRefundTransaction: `sale.OrderStatus = "Refunded"; ... product.StockQuantity += item.Quantity;`; [SwiftCheckoutPOS.BLL/Services/RefundService.cs](../SwiftCheckoutPOS.BLL/Services/RefundService.cs), lines 61–62 — ExecuteRefund: `_salesRepo.ProcessRefundTransaction(sale);` |
| DR-16 · High | 33 | Approved category/type allowlists; four system-only inventory/cost categories — Old Capital/Loan/Misc labels, incomplete reserved checks and unknown type coercion conflict with approved categories. | [SwiftCheckoutPOS.BLL/Services/FinanceService.cs](../SwiftCheckoutPOS.BLL/Services/FinanceService.cs), lines 114–135 — RecordManualEntry: `TransactionType = type == "Inflow" ? "Inflow" : "Outflow"`; [SwiftCheckoutPOS.UI/Views/FinanceAdmin/Index.cshtml](../SwiftCheckoutPOS.UI/Views/FinanceAdmin/Index.cshtml), lines 344–355 — category options: `{ value: 'Capital', ... }` |
| DR-17 · Critical | 34,35,36 | Net Cash Position, approved Opex only, VAT excluded from revenue/profit — NCP omits sales/other inflows/refunds; period Opex includes financing; lifetime revenue uses tax-inclusive TotalAmount. | [SwiftCheckoutPOS.BLL/Services/FinanceService.cs](../SwiftCheckoutPOS.BLL/Services/FinanceService.cs), lines 54–82 — GetFinanceDashboard: `NetLedgerBalance = Math.Round(capital - (inventoryCash + opex), 2)` |
| DR-18 · Critical | 37,38 | Historical CostAtSale; unknown historical costs shown/excluded, never invented — Restored completion writes CostAtSale (CheckoutService 294), but readers still use today's cost or zero and have no unknown marker. | [SwiftCheckoutPOS.BLL/Services/ReportService.cs](../SwiftCheckoutPOS.BLL/Services/ReportService.cs), lines 47–52 — GetAdvancedReport: `(si.Product?.CostPrice ?? 0) * si.Quantity`; [SwiftCheckoutPOS.BLL/Services/CustomerService.cs](../SwiftCheckoutPOS.BLL/Services/CustomerService.cs), lines 61,142,161 — profile/history: `si.Product?.CostPrice ?? 0`; [SwiftCheckoutPOS.BLL/Services/DashboardService.cs](../SwiftCheckoutPOS.BLL/Services/DashboardService.cs), lines 46,76 — GetMetrics: `si.Product?.CostPrice ?? 0` |
| DR-19 · Medium | 39,40,81 | Capital payback label; earliest first capital/sale; elapsed days/30.44 minimum 1 — Capital wins even if later; integer month difference and positive-profit clamp; old break-even naming. | [SwiftCheckoutPOS.BLL/Services/FinanceService.cs](../SwiftCheckoutPOS.BLL/Services/FinanceService.cs), lines 85–107 — GetFinanceDashboard: `firstCapitalDate ?? (firstSaleDate ?? DateTime.Today)`; [SwiftCheckoutPOS.UI/Views/FinanceAdmin/Index.cshtml](../SwiftCheckoutPOS.UI/Views/FinanceAdmin/Index.cshtml), lines 169–204 — payback card: `Model.EstimatedMonthsToBreakEven` |
| DR-20 · Medium | 58,59,79,80 | Asia/Dhaka half-open business days; KPI date-only, table search-only; All time label — Host-local dates and inclusive last-tick queries; search changes KPI inputs. Lifetime queries ignore dates, but labels vary. | [SwiftCheckoutPOS.BLL/Services/ReportService.cs](../SwiftCheckoutPOS.BLL/Services/ReportService.cs), lines 22–45 — GetAdvancedReport: `allSales = allSales.Where(...)`; [SwiftCheckoutPOS.BLL/Services/FinanceService.cs](../SwiftCheckoutPOS.BLL/Services/FinanceService.cs), lines 26–40,73–74 — GetFinanceDashboard: `rawEntries = rawEntries.Where(...)`; [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 302 — SetSale: `sale.SaleDate = DateTime.Now;` |
| DR-21 · High | 41,42,43,44,88 | User-entered float; one explicit open shift; blind count; shift cash movements and attributed discrepancy — No real shift open/paid-in/out; competing expected-cash formulas; hardcoded actor and form binding mismatch. No >1000 approval fact. | [SwiftCheckoutPOS.BLL/Services/TillReconciliationService.cs](../SwiftCheckoutPOS.BLL/Services/TillReconciliationService.cs), lines 17,25–45 — GenerateCurrentShiftPreview: `DefaultOpeningFloat = 5000.00m`; [SwiftCheckoutPOS.BLL/Services/ReconciliationService.cs](../SwiftCheckoutPOS.BLL/Services/ReconciliationService.cs), lines 17–28 — PerformBlindReconciliation: `OpenTime = DateTime.Now.AddHours(-8)`; [SwiftCheckoutPOS.UI/Controllers/ReconciliationController.cs](../SwiftCheckoutPOS.UI/Controllers/ReconciliationController.cs), lines 30–38 — CloseTill: `int userId = 1;` |
| DR-22 · High | 51,64 | 15-minute password unlock preserving cart; 12h maximum ticket — 8h ticket/session and no inactivity-lock/unlock execution path. | [SwiftCheckoutPOS.UI/Program.cs](../SwiftCheckoutPOS.UI/Program.cs), lines 24–40 — session/cookie setup: `TimeSpan.FromHours(8)`; [SwiftCheckoutPOS.UI/Controllers/AccountController.cs](../SwiftCheckoutPOS.UI/Controllers/AccountController.cs), lines 37–61 — Login: `SignInAsync("POSAuthCookie", ...)` |
| DR-23 · High | 78 | After five failures, 30s delay increasing to 5min, no permanent lockout; audit failures — No failed-attempt/delay counters or logged failed-login path. | [SwiftCheckoutPOS.BLL/Services/AuthService.cs](../SwiftCheckoutPOS.BLL/Services/AuthService.cs), lines 18–44 — ValidateCredentials: `if (!PasswordHasher.Verify(...)) return false;`; [SwiftCheckoutPOS.DAL/Repositories/UserRepository.cs](../SwiftCheckoutPOS.DAL/Repositories/UserRepository.cs), lines 30–38 — lookup: `GetUserByUsername` |
| DR-24 · High | 50 | Admin inherits Manager operational permissions; cashier customer-add/own till — Admin excluded from Manager dashboard; cashier customer-add and own till not available. | [SwiftCheckoutPOS.UI/Controllers/ManagerDashboardController.cs](../SwiftCheckoutPOS.UI/Controllers/ManagerDashboardController.cs), lines 8–13 — Index: `[Authorize(Roles = "Manager")]`; [SwiftCheckoutPOS.UI/Controllers/CustomerController.cs](../SwiftCheckoutPOS.UI/Controllers/CustomerController.cs), lines 10,29–35 — Add: `[Authorize(Roles = "Admin,Manager")]`; [SwiftCheckoutPOS.UI/Controllers/ReportController.cs](../SwiftCheckoutPOS.UI/Controllers/ReportController.cs), lines 10,33–46 — TillCount/SubmitTillCount: `[Authorize(Roles = "Admin")]` |
| DR-25 · Critical | 50,92 | Financial view Admin-only by default; audited Manager toggle; protected projection; backup always Admin — No setting/policy/projection boundary; Manager sees financial pages/fields and can download entire DB. Full surface register is roadmap BP-15. | [SwiftCheckoutPOS.UI/Controllers/FinanceAdminController.cs](../SwiftCheckoutPOS.UI/Controllers/FinanceAdminController.cs), lines 10,21–31 — Index: `[Authorize(Roles = "Admin,Manager")]`; [SwiftCheckoutPOS.UI/Controllers/BackupAdminController.cs](../SwiftCheckoutPOS.UI/Controllers/BackupAdminController.cs), lines 12,63–75 — DownloadBackup: `return File(fileBytes, ...);` |
| DR-26 · High | 45,58,79 | SCP-YYYYMMDD-0001 at completion, sequential Dhaka business day, never reused — Random receipt assigned when cart is created, also used by holds/idempotency; no daily counter. | [SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs](../SwiftCheckoutPOS.BLL/DTOs/CheckoutSessionDTO.cs), lines 12,70–71 — CreateInvoiceNumber: `INV-{DateTime.UtcNow:yyyyMMddHHmmss}-...`; [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 235–236 — CompleteSale: `ReceiptNumber = command.InvoiceNumber` |
| DR-27 · High | 46,85 | Up to two tender allocations: Cash/Card/bKash/Nagad/Due; payment confirmation/reference and change — Single string/total, undifferentiated wallet; no allocations, received reference, paid/change facts. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 396–400 — ValidatePayment: `payment != "Cash" && payment != "Card" && payment != "Mobile Wallet"`; [SwiftCheckoutPOS.DAL/Entities/Sale.cs](../SwiftCheckoutPOS.DAL/Entities/Sale.cs), lines 28–34 — Sale: `public string PaymentMethod { get; set; }` |
| DR-28 · High | 86 | Registered-customer Due ≤2500; approval per sale; balance and collections retained — No receivable balance, Due allocation, approval or collection history. | [SwiftCheckoutPOS.DAL/Entities/Customer.cs](../SwiftCheckoutPOS.DAL/Entities/Customer.cs), lines 10–17 — Customer fields: `public int LoyaltyPoints { get; set; }`; [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 396–400 — ValidatePayment: `Choose Cash, Card or Mobile Wallet.` |
| DR-29 · High | 87 | Manager/Admin void within five minutes with reason; restore stock, retain Voided sale — Only uncompleted local/resumed cart cancellation; no completed-sale void action/reason/time/financial effects. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 15–18,200–218 — states/CancelCart: `Held / Resumed / Completed / Cancelled`; [SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs](../SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs), lines 118–129 — ClearCart: `checkoutService.CancelCart(cart);` |
| DR-30 · Medium | 47 | Drawer for cash sale/refund only; Manager/Admin No sale with reason/audit — Postcommit failure isolation is present, but drawer always triggers regardless of tender; no No sale reason path. | [SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs](../SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs), lines 207–223 — PrintCommittedSale: `hardwarePrinter.OpenCashDrawer();` |
| DR-31 · High | 10,48,83 | Admin-editable header; VAT/BIN omitted now; saved-sale receipts; authorized logged REPRINT/COPY — Hardcoded header/BIN/footer, cart-derived result receipt, no saved-history reprint/marking/actor log or paid/change/rounding rows. | [SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs](../SwiftCheckoutPOS.BLL/Services/VirtualPrinterService.cs), lines 80–95,117–128 — BuildReceiptString: `CenterText("SWIFT SUPERMARKET", paperWidth)`; [SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs](../SwiftCheckoutPOS.UI/Controllers/CheckoutController.cs), lines 148–153,185–190 — print/finalize: `PrintCommittedSale(result.Receipt)` |
| DR-32 · High | 49,67,82,83,87,92,93 | Sensitive actions keep actor/time/reason/approval and safe old/new values — Sale/ledger actor alone is not a sensitive-action or hold/resume/settings/reprint history. | [SwiftCheckoutPOS.BLL/Services/CheckoutService.cs](../SwiftCheckoutPOS.BLL/Services/CheckoutService.cs), lines 157,191,308 — hold/resume/SetSale: `sale.CashierName = actor;`; [SwiftCheckoutPOS.DAL/Entities/LedgerEntry.cs](../SwiftCheckoutPOS.DAL/Entities/LedgerEntry.cs), lines 13–18 — LedgerEntry: `public string? RecordedBy { get; set; }` |

Financial privacy has **18 current-surface records FP-01–18**, including every requested controller/action, DTO field group, view column/output and current role. It distinguishes rendered leaks from a rich server-only DTO and placeholder pages. BP-15 / OAT-34 require OFF/ON/direct-URL/error/sort/download tests, no private browser fields, operational exceptions and always-Admin backup.

## 4. Contradictions / unclear answers — questions, not resolutions

Only PR-Q63 is formally PENDING; these approved-answer interactions still require clarification before affected implementation.

| Question | Related PR-Q | Question for the owner |
| --- | --- | --- |
| CQ-01 | PR-Q07/85/91 | For a Cash+Card/bKash/Nagad/Due split, which amount is rounded: the full payable or the cash allocation, and how is the rounding difference allocated? The answers define cash-only rounding but not mixed splits. |
| CQ-02 | PR-Q05/67/90/93 | What is the combined-discount percentage denominator, and how are bill discounts allocated to each item's net-cost floor (including allocation/discount rounding)? Item+bill combined limits are approved; this exact comparison basis is not specified. |
| CQ-03 | PR-Q22/50/92/93 | With financial view OFF, may a Manager approve a below-cost override without seeing cost, or must Admin approve as the PR-Q92 price message says? How is initial cost entered for a Manager-created product when cost fields are forbidden? |
| CQ-04 | PR-Q15/16/17/49 | After resume into an active cart, must the reservation stay in place until completion/cancel, or be released as the restored code does? The answers require held reservations and cancel releases, without stating the resume boundary. |
| CQ-05 | PR-Q28/30/65/66/85/86 | For split or partly unpaid Due sales, how are refunds allocated across original tenders, unpaid due reduction and already-collected due? No payout may be invented for an uncollected receivable. |
| CQ-06 | PR-Q29/65/66 | For damaged refunded goods, is original COGS reversed before the Damaged expense, or does it remain? “Expense instead” and “COGS reversal if restocked” leave the risk of double-expensing unclear. |
| CQ-07 | PR-Q24/33/34/66 | Does Net Cash Position exclude noncash COGS/valuation adjustments, and how are refund payouts prevented from being counted both separately and in ledger outflows? The literal “all ledger” formula conflicts with noncash/system and linked-refund postings. |
| CQ-08 | PR-Q88 | Is the BDT1000 approval threshold applied to absolute over/short magnitude (including a negative shortage), strictly greater than 1000? The answer says “differences above” without a signed/magnitude rule. |
| CQ-09 | PR-Q78 | After the fifth failed login, what exact sequence increases the 30-second delay to five minutes, and when do attempt counters reset? |
| CQ-10 | PR-Q34/37/50/86/92 | Who may view customer due balances and collect due when financial view is OFF, which tenders may collect it, and when is Due-sale revenue/profit recognized? The due cap/approval are approved; these operational/reporting boundaries are not. |
| CQ-11 | PR-Q87/28/30/65/66 | What payment, VAT, COGS and due reversal entries accompany a void within five minutes? Stock/status/reason/time are approved, but money already received cannot be assumed reversed by status alone. |
| CQ-12 | PR-Q41–44/88 | Must every sale/due collection/refund require an open shift, who owns cross-user cash movements, and can the cashier see expected/discrepancy after blind submission? |
| CQ-13 | PR-Q27/45/63 | Does “within seven days” mean elapsed 168 hours or business dates, and what is the receipt-sequence policy after 9999 completions in one business day? |
| CQ-14 | PR-Q39/40/81 | When average profit is zero or negative, what must the Capital payback estimate display? The current code returns zero months; the approved name/origin/month denominator do not define this edge case. |
| CQ-15 | PR-Q50/92 | Does protected period “sales totals” also include sales counts/quantities and customer aggregate discounts/refunds, or only monetary sales revenue? No new visibility exception is inferred here. |
| CQ-16 | PR-Q07/30/31/34/36/87/91 | How is the saved cash rounding difference treated in revenue/profit/ledger, and how is it reversed or allocated for full/partial refunds and voids? Storage/receipt display and cash-only scope are approved, but these financial effects are not stated. |

No old CHECK-marked draft answers were merged: the final owner-confirmed version removes those markers. Examples in OAT-09/13/15/17/18 are explicitly **“Derived from PR-Qxx — owner to confirm”** and use synthetic fixture numbers,0% current VAT and no invented standard rate. Future VAT extraction example cannot enable registered mode without updating PR-Q10. OAT-34 is the manual full privacy matrix; OAT-35–38 cover new payment/void/discount/receipt scope. All38 cases remain NOT EXECUTED.

## 5. Link and preservation validation

Final verification results are recorded below. All workspace links use relative GitHub targets and original line numbers are plain text after the link. Historical command strings can still contain literal local paths; they are not Markdown links and are preserved as evidence. The ignored/untracked BackupAdmin view is captured as inert numbered source in EVIDENCE_REFERENCES.md; historical generated NuGet asset references point to provenance/quotations there, not a fabricated current copy of the old file.

- PR-Q rows: **93**; **92 ANSWERED / 1 OPEN (PR-Q63)**; unique IDs PR-Q01–93 and explicit OWNER_DECISIONS.md sources.
- Current differences: **32**; privacy surfaces: **18**; roadmap tasks: **42**; playbook mappings: **15**; acceptance cases: **38, all unexecuted**.
- Remaining absolute C:/source Markdown links: **0**. All relative targets resolve to tracked files or new docs in this change; Markdown/explicit HTML anchors checked.
- PROJECT_AUDIT.md exactly equals the original text after only the defined hyperlink/line-reference transformations and evidence-link routing: **PASS**.
- OWNER_DECISIONS.md exactly equals the original after removing the one merge line: **PASS**.
- Owner NuGet reports unchanged: **PASS**.
- The **202 fingerprinted non-doc workspace files** and parent AGENTS bytes match the post-input baseline. Git separately reports an unrelated deleted test AGENTS file; this task did not edit/restore it. No code/test/schema/config/package mutation by this work.
- No app, test, build, database or package command executed. Report statuses are evidence from file reads, not a runtime readiness certificate.

## 6. Git scope

Git root is the parent repo. Baseline status initially included an owner-provided **M AGENTS.md**; this task did not edit it. The final documentation-scoped status below lists this task's ten docs files. The full project status also reports **D SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/AGENTS.md**, an unrelated change; an earlier read had already found that file missing before documentation writes. It was not edited, deleted or restored by this task. Therefore the overall worktree cannot honestly be described as docs-only; the task's own writes are docs-only.

```text
 M SwiftCheckoutPOS/docs/ARCHITECTURE.md
 M SwiftCheckoutPOS/docs/CHECKOUT_RESTORATION.md
 M SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md
 M SwiftCheckoutPOS/docs/DEVELOPMENT_ROADMAP.md
 M SwiftCheckoutPOS/docs/OFFLINE_ACCEPTANCE_TESTS.md
 M SwiftCheckoutPOS/docs/OWNER_DECISIONS.md
 M SwiftCheckoutPOS/docs/PRODUCT_REQUIREMENTS.md
 M SwiftCheckoutPOS/docs/PROJECT_AUDIT.md
?? SwiftCheckoutPOS/docs/EVIDENCE_REFERENCES.md
?? SwiftCheckoutPOS/docs/OWNER_DECISIONS_MERGE_REPORT.md
```

Unrelated project status (left untouched under the explicit AGENTS restriction):

```text
 D SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/AGENTS.md
```

## 7. Read-only commands and output summary

No dotnet/build/test/package/database command was executed. File reads/source searches, hashing and Git read-only status/list were used. All final source comparisons use122 C#/Razor/project files plus the inspected test-project declaration; hashing is scope evidence, not a claim that every vendor asset was semantically audited. Source review focused on financial/privacy/auth/checkout/refund/stock/report/till paths.

The command log includes early reads made while the owner's replacement files were not yet visible; later full rereads superseded them. Expected missing-file/search/Git-parent sandbox errors were recorded, not interpreted as absence after the updated files arrived. Parent Git status/list required read-only sandbox elevation; optional locks were disabled and no Git index/config/commit was written.


### Read command 01

```powershell
$docsInstructionPaths=@('C:/AGENTS.md','C:/source/AGENTS.md','C:/source/repos/AGENTS.md','C:/source/repos/SwiftCheckoutPOS/AGENTS.md','C:/source/repos/SwiftCheckoutPOS/docs/AGENTS.md'); foreach($docsInstructionPath in $docsInstructionPaths){ if(Test-Path -LiteralPath $docsInstructionPath -PathType Leaf){ Write-Output "FILE: $docsInstructionPath"; Get-Content -LiteralPath $docsInstructionPath -Raw } }; Get-ChildItem -LiteralPath docs -Recurse -File | Select-Object FullName,Length | ConvertTo-Json; git status --short; git rev-parse --show-toplevel
```

### Read command 02

```powershell
Get-Content -LiteralPath docs/OWNER_DECISIONS.md -Raw
```

### Read command 03

```powershell
$decisionPaths=@('docs/PRODUCT_REQUIREMENTS.md','docs/DEVELOPMENT_ROADMAP.md'); foreach($decisionPath in $decisionPaths){ Write-Output "FILE: $decisionPath"; Get-Content -LiteralPath $decisionPath -Raw }
```

### Read command 04

```powershell
Get-Content -LiteralPath docs/ARCHITECTURE.md -Raw
```

### Read command 05

```powershell
$decisionPaths=@('docs/DATA_RECOVERY_PLAN.md','docs/OFFLINE_ACCEPTANCE_TESTS.md','docs/CHECKOUT_RESTORATION.md'); foreach($decisionPath in $decisionPaths){ Write-Output "FILE: $decisionPath"; Get-Content -LiteralPath $decisionPath -Raw }
```

### Read command 06

```powershell
Get-Content -LiteralPath docs/PROJECT_AUDIT.md -Raw
```

### Read command 07

```powershell
Get-Location; Get-Command git | Select-Object Name,Source,CommandType; Get-ChildItem -LiteralPath . -Force | Select-Object Name,Mode; $decisionGitPaths=@('.git','C:/source/repos/.git','C:/source/.git'); foreach($decisionGitPath in $decisionGitPaths){ if(Test-Path -LiteralPath $decisionGitPath){ Get-Item -LiteralPath $decisionGitPath -Force | Select-Object FullName,Mode,Length } }; git --version; git -C C:/source/repos/SwiftCheckoutPOS status --short; $decisionFiles=rg --files --hidden -g '!docs/**' -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/.vs/**'; $decisionHashes=foreach($decisionFile in $decisionFiles){ '{0}|{1}' -f $decisionFile,(Get-FileHash -LiteralPath $decisionFile -Algorithm SHA256).Hash }; $decisionBytes=[Text.Encoding]::UTF8.GetBytes((($decisionHashes | Sort-Object) -join "`n")); $decisionHasher=[Security.Cryptography.SHA256]::Create(); [pscustomobject]@{OutsideDocsFileCount=@($decisionFiles).Count;OutsideDocsSHA256=([BitConverter]::ToString($decisionHasher.ComputeHash($decisionBytes))).Replace('-','')} | ConvertTo-Json
```

### Read command 08

```powershell
rg -n '^\s*\[(Authorize|AllowAnonymous|HttpGet|HttpPost)|public (async )?(Task<.*>|IActionResult|ActionResult).*\(' SwiftCheckoutPOS.UI/Controllers; $decisionProjects=@('SwiftCheckoutPOS.UI/SwiftCheckoutPOS.UI.csproj','SwiftCheckoutPOS.BLL/SwiftCheckoutPOS.BLL.csproj','SwiftCheckoutPOS.DAL/SwiftCheckoutPOS.DAL.csproj'); foreach($decisionProject in $decisionProjects){ Write-Output "FILE: $decisionProject"; Select-String -LiteralPath $decisionProject -Pattern 'TargetFramework|PackageReference|ProjectReference' }; $decisionReports=@('docs/nuget-vulnerable.txt','docs/nuget-outdated.txt'); foreach($decisionReport in $decisionReports){ if(Test-Path -LiteralPath $decisionReport){ Get-Item -LiteralPath $decisionReport | Select-Object FullName,LastWriteTime,Length; Get-Content -LiteralPath $decisionReport -Raw }else{ Write-Output "MISSING: $decisionReport" } }; rg -n 'T0[1-9]|T1[0-5]|Step 4|Playbook' docs
```

### Read command 09

```powershell
$decisionUpdatedPaths=@('AGENTS.md','docs/OWNER_DECISIONS.md','docs/nuget-vulnerable.txt','docs/nuget-outdated.txt'); foreach($decisionUpdatedPath in $decisionUpdatedPaths){ Write-Output "FILE: $decisionUpdatedPath"; if(Test-Path -LiteralPath $decisionUpdatedPath -PathType Leaf){ Get-Content -LiteralPath $decisionUpdatedPath -Raw }else{ Write-Output 'MISSING' } }
```

### Read command 10

```powershell
$decisionPaths=@('C:/source/repos/AGENTS.md','C:/source/repos/SwiftCheckoutPOS/AGENTS.md','C:/source/repos/SwiftCheckoutPOS/docs/OWNER_DECISIONS.md','C:/source/repos/SwiftCheckoutPOS/docs/nuget-vulnerable.txt','C:/source/repos/SwiftCheckoutPOS/docs/nuget-outdated.txt'); foreach($decisionPath in $decisionPaths){ if(Test-Path -LiteralPath $decisionPath -PathType Leaf){ Get-Item -LiteralPath $decisionPath | Select-Object FullName,Length,LastWriteTimeUtc }else{ Write-Output "MISSING: $decisionPath" } }; rg --files --hidden -g OWNER_DECISIONS.md -g AGENTS.md -g nuget-vulnerable.txt -g nuget-outdated.txt -g '!**/.git/**' -g '!**/.vs/**' C:/source/repos
```

### Read command 11

```powershell
Get-Content -LiteralPath C:/source/repos/AGENTS.md -Raw
```

### Read command 12

```powershell
$decisionSourcePaths=rg --files SwiftCheckoutPOS.UI SwiftCheckoutPOS.BLL SwiftCheckoutPOS.DAL -g '*.cs' -g '*.cshtml' -g '*.csproj' -g '!**/bin/**' -g '!**/obj/**'; $decisionSources=foreach($decisionSourcePath in $decisionSourcePaths){ [pscustomobject]@{Path=$decisionSourcePath.Replace('\','/');Text=(Get-Content -LiteralPath $decisionSourcePath -Raw)} }; $decisionSources | ConvertTo-Json -Depth 4 -Compress
```

### Read command 13

```powershell
$decisionUpdatedPaths=@('C:/source/repos/AGENTS.md','C:/source/repos/SwiftCheckoutPOS/AGENTS.md','docs/OWNER_DECISIONS.md','docs/nuget-vulnerable.txt','docs/nuget-outdated.txt'); foreach($decisionUpdatedPath in $decisionUpdatedPaths){ Write-Output "FILE: $decisionUpdatedPath"; if(Test-Path -LiteralPath $decisionUpdatedPath -PathType Leaf){ Get-Content -LiteralPath $decisionUpdatedPath -Raw }else{ Write-Output 'MISSING' } }
```

### Read command 14

```powershell
$decisionPaths=@('C:/source/repos/AGENTS.md','docs/OWNER_DECISIONS.md','docs/nuget-vulnerable.txt','docs/nuget-outdated.txt'); foreach($decisionPath in $decisionPaths){ Get-Item -LiteralPath $decisionPath | Select-Object FullName,Length,LastWriteTimeUtc }; rg --files --hidden -g '*PLAYBOOK*' -g '*Playbook*' -g '*playbook*' -g '*.md' -g '!**/.git/**' -g '!**/.vs/**' -g '!**/bin/**' -g '!**/obj/**' C:/source/repos; git --no-optional-locks --git-dir=C:/source/repos/.git --work-tree=C:/source/repos status --short --untracked-files=all
```

### Read command 15

```powershell
$decisionFiles=rg --files --hidden -g '!docs/**' -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/.vs/**'; $decisionHashes=foreach($decisionFile in $decisionFiles){ '{0}|{1}' -f $decisionFile,(Get-FileHash -LiteralPath $decisionFile -Algorithm SHA256).Hash }; $decisionBytes=[Text.Encoding]::UTF8.GetBytes((($decisionHashes | Sort-Object) -join "`n")); $decisionHasher=[Security.Cryptography.SHA256]::Create(); [pscustomobject]@{OutsideDocsFileCount=@($decisionFiles).Count;OutsideDocsSHA256=([BitConverter]::ToString($decisionHasher.ComputeHash($decisionBytes))).Replace('-','');ParentAgentsSHA256=(Get-FileHash -LiteralPath C:/source/repos/AGENTS.md -Algorithm SHA256).Hash} | ConvertTo-Json
```

### Read command 16

```powershell
Get-Content -LiteralPath C:/source/repos/README.md -Raw
```

### Read command 17

```powershell
$decisionDocPaths=Get-ChildItem -LiteralPath docs -Recurse -File; $decisionDocRows=foreach($decisionDocPath in $decisionDocPaths){ $decisionRaw=[IO.File]::ReadAllBytes($decisionDocPath.FullName); [pscustomobject]@{Name=$decisionDocPath.Name;Text=[Text.Encoding]::UTF8.GetString($decisionRaw);ByteLength=$decisionRaw.Length;LastWriteTimeUtc=$decisionDocPath.LastWriteTimeUtc.ToString('o')} }; $decisionDocRows | ConvertTo-Json -Depth 4 -Compress
```

### Read command 18

```powershell
rg --files SwiftCheckoutPOS.UI/Views -g '*Backup*' -g '*.cshtml'; Get-Content -LiteralPath SwiftCheckoutPOS.UI/Views/BackupAdmin/Index.cshtml -Raw -ErrorAction Continue; Get-Content -LiteralPath Tests/SwiftCheckoutPOS.Tests.Unit/AGENTS.md -Raw; Select-String -LiteralPath Tests/SwiftCheckoutPOS.Tests.Unit/SwiftCheckoutPOS.Tests.Unit.csproj -Pattern 'TargetFramework|ProjectReference|PackageReference'
```

### Read command 19

```powershell
$decisionSourcePaths=rg --files --hidden --no-ignore SwiftCheckoutPOS.UI SwiftCheckoutPOS.BLL SwiftCheckoutPOS.DAL -g '*.cs' -g '*.cshtml' -g '*.csproj' -g '!**/bin/**' -g '!**/obj/**'; $decisionSources=foreach($decisionSourcePath in $decisionSourcePaths){ [pscustomobject]@{Path=$decisionSourcePath.Replace('\','/');Text=(Get-Content -LiteralPath $decisionSourcePath -Raw)} }; $decisionSources | ConvertTo-Json -Depth 4 -Compress
```

### Read command 20

```powershell
$decisionFiles=rg --files --hidden --no-ignore -g '!docs/**' -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/.vs/**'; $decisionHashes=foreach($decisionFile in $decisionFiles){ '{0}|{1}' -f $decisionFile,(Get-FileHash -LiteralPath $decisionFile -Algorithm SHA256).Hash }; $decisionBytes=[Text.Encoding]::UTF8.GetBytes((($decisionHashes | Sort-Object) -join "`n")); $decisionHasher=[Security.Cryptography.SHA256]::Create(); [pscustomobject]@{OutsideDocsFileCount=@($decisionFiles).Count;OutsideDocsSHA256=([BitConverter]::ToString($decisionHasher.ComputeHash($decisionBytes))).Replace('-','');ParentAgentsSHA256=(Get-FileHash -LiteralPath C:/source/repos/AGENTS.md -Algorithm SHA256).Hash} | ConvertTo-Json
```

### Read command 21

```powershell
Get-Content -LiteralPath C:/source/repos/AGENTS.md -Raw; Get-Item -LiteralPath C:/source/repos/AGENTS.md | Select-Object Length,LastWriteTimeUtc; Get-Content -LiteralPath Tests/SwiftCheckoutPOS.Tests.Unit/AGENTS.md -Raw -ErrorAction SilentlyContinue
```

### Read command 22

```powershell
git --no-optional-locks -C C:/source/repos status --short --untracked-files=all
```

### Read command 23

```powershell
git --no-optional-locks -C C:/source/repos ls-files
```

### Read command 24

```powershell
$decisionDocRows=Get-ChildItem -LiteralPath docs -Recurse -File | ForEach-Object { [pscustomobject]@{Name=$_.Name;Text=[IO.File]::ReadAllText($_.FullName)} }; $decisionDocRows | ConvertTo-Json -Depth 4 -Compress
```

## 8. Final scope/link commands

These commands wrote only documentation link/text updates or read status/fingerprints. Git commands are read-only with optional locks disabled; source/package/configuration files were never write targets.

```powershell
$decisionPrefix='C:/source/repos/SwiftCheckoutPOS/'; $decisionPattern='\[([^\]]+)\]\(C:/source/repos/SwiftCheckoutPOS/([^)]+)\)'; $decisionEncoding=[Text.UTF8Encoding]::new($false); $decisionConversionRows=foreach($decisionFile in Get-ChildItem -LiteralPath docs -Recurse -File){ $decisionOriginal=[IO.File]::ReadAllText($decisionFile.FullName); $decisionMatches=[regex]::Matches($decisionOriginal,$decisionPattern).Count; if($decisionMatches -eq 0){ continue }; $decisionConverted=[regex]::Replace($decisionOriginal,$decisionPattern,[Text.RegularExpressions.MatchEvaluator]{ param($decisionMatch) $decisionLabel=$decisionMatch.Groups[1].Value; $decisionTarget=$decisionMatch.Groups[2].Value; $decisionLine=''; if($decisionTarget -match ':(\d+(?:[–-]\d+)?)$'){ $decisionLine=$Matches[1]; $decisionTarget=$decisionTarget.Substring(0,$decisionTarget.Length-$Matches[0].Length) }; if($decisionLabel -match ':(\d+(?:[–-]\d+)?)$'){ $decisionLine=$Matches[1]; $decisionLabel=$decisionLabel.Substring(0,$decisionLabel.Length-$Matches[0].Length) } elseif($decisionLabel -match '^\d+(?:[–-]\d+)?$'){ $decisionLine=$decisionLabel; $decisionLabel=$decisionTarget.Split('/')[-1] }; $decisionRelative=if($decisionTarget.StartsWith('docs/')){ $decisionTarget.Substring(5) }else{ '../'+$decisionTarget }; if($decisionRelative.Contains(' ')){ $decisionRelative='<'+$decisionRelative+'>' }; '['+$decisionLabel+']('+$decisionRelative+')'+$(if($decisionLine){ ' (lines '+$decisionLine+')' }else{ '' }) }); [IO.File]::WriteAllText($decisionFile.FullName,$decisionConverted,$decisionEncoding); [pscustomobject]@{File=$decisionFile.Name;LinksConverted=$decisionMatches} }; $decisionConversionRows | ConvertTo-Json
```

```powershell
$decisionEncoding=[Text.UTF8Encoding]::new($false); foreach($decisionFile in Get-ChildItem -LiteralPath docs -File -Filter '*.md'){ $decisionOriginal=[IO.File]::ReadAllText($decisionFile.FullName); $decisionUpdated=$decisionOriginal.Replace('](C:/source/repos/AGENTS.md)','](../../AGENTS.md)').Replace('](../SwiftCheckoutPOS.UI/Views/BackupAdmin/Index.cshtml)','](EVIDENCE_REFERENCES.md#backupadmin-view-snapshot)').Replace('](../SwiftCheckoutPOS.DAL/obj/project.assets.json)','](EVIDENCE_REFERENCES.md#generated-nuget-asset-evidence)'); if($decisionUpdated -ne $decisionOriginal){ [IO.File]::WriteAllText($decisionFile.FullName,$decisionUpdated,$decisionEncoding); Write-Output $decisionFile.Name } }
```

```powershell
$decisionEncoding=[Text.UTF8Encoding]::new($false); foreach($decisionPath in @('docs/DEVELOPMENT_ROADMAP.md','docs/OFFLINE_ACCEPTANCE_TESTS.md')){ $decisionText=[IO.File]::ReadAllText((Join-Path (Get-Location) $decisionPath)); $decisionUpdated=$decisionText.Replace('CQ-01–14','CQ-01–15'); if($decisionText -ne $decisionUpdated){ [IO.File]::WriteAllText((Join-Path (Get-Location) $decisionPath),$decisionUpdated,$decisionEncoding) } }
```

```powershell
$decisionEncoding=[Text.UTF8Encoding]::new($false); foreach($decisionPath in @('docs/DEVELOPMENT_ROADMAP.md','docs/OFFLINE_ACCEPTANCE_TESTS.md')){ $decisionText=[IO.File]::ReadAllText((Join-Path (Get-Location) $decisionPath)); $decisionUpdated=$decisionText.Replace('CQ-01–15','CQ-01–16'); if($decisionText -ne $decisionUpdated){ [IO.File]::WriteAllText((Join-Path (Get-Location) $decisionPath),$decisionUpdated,$decisionEncoding) } }
```

```powershell
$decisionEncoding=[Text.UTF8Encoding]::new($false); $decisionPath=Join-Path (Get-Location) 'docs/DEVELOPMENT_ROADMAP.md'; $decisionText=[IO.File]::ReadAllText($decisionPath); $decisionUpdated=$decisionText.Replace('CQ-01–12 block only affected final semantics.','CQ-01–16 block only affected final semantics.'); if($decisionText -ne $decisionUpdated){ [IO.File]::WriteAllText($decisionPath,$decisionUpdated,$decisionEncoding) }
```

```powershell
$decisionFiles=rg --files --hidden --no-ignore -g '!docs/**' -g '!.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/.vs/**'; $decisionHashes=foreach($decisionFile in $decisionFiles){ '{0}|{1}' -f $decisionFile,(Get-FileHash -LiteralPath $decisionFile -Algorithm SHA256).Hash }; $decisionBytes=[Text.Encoding]::UTF8.GetBytes((($decisionHashes | Sort-Object) -join "`n")); $decisionHasher=[Security.Cryptography.SHA256]::Create(); [pscustomobject]@{OutsideDocsFileCount=@($decisionFiles).Count;OutsideDocsSHA256=([BitConverter]::ToString($decisionHasher.ComputeHash($decisionBytes))).Replace('-','');ParentAgentsSHA256=(Get-FileHash -LiteralPath C:/source/repos/AGENTS.md -Algorithm SHA256).Hash} | ConvertTo-Json
```

```powershell
git --no-optional-locks -C C:/source/repos status --short --untracked-files=all -- SwiftCheckoutPOS
```

```powershell
git --no-optional-locks -C C:/source/repos status --short --untracked-files=all -- SwiftCheckoutPOS/docs
```

```powershell
git --no-optional-locks -C C:/source/repos diff --name-status -- SwiftCheckoutPOS/Tests/SwiftCheckoutPOS.Tests.Unit/AGENTS.md; git --no-optional-locks -C C:/source/repos status --short --untracked-files=all
```


## 9. Areas not inspected / evidence limits

No business/test database connection, actual deployed schema or rows, SQL edition/version/grants/recovery model, service/certificate/key/ACL state, physical hardware, restore/RPO/RTO measurement, app boot/network behavior or acceptance execution. No package advisory/deprecation refresh or new Git-history credential review. Third-party JS/CSS internals and new test behavior were not re-audited; prior audit/restoration evidence remains historical. No real credentials/connection values were printed. Relative links to generated evidence are transparent provenance, not a claim that a full historical assets file is available.

## 10. CQ merge

Merged **2026-10-31 (Asia/Dhaka)** from immutable [OWNER_DECISIONS.md](OWNER_DECISIONS.md), section N. **CQ-01–CQ-16:16 ANSWERED /0 OPEN**, each with a one-line answer/source in [requirements section7](PRODUCT_REQUIREMENTS.md#7-clarification-answers-remaining-questions-and-change-control). PR register unchanged in status:92 ANSWERED / PR-Q63 OPEN. Hardware models remain approved deferrals. No code, tests, configuration, packages, app or database operation; all acceptance examples are unexecuted.

Files changed: [ARCHITECTURE.md](ARCHITECTURE.md), [DATA_RECOVERY_PLAN.md](DATA_RECOVERY_PLAN.md), [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md), [EVIDENCE_REFERENCES.md](EVIDENCE_REFERENCES.md), [OFFLINE_ACCEPTANCE_TESTS.md](OFFLINE_ACCEPTANCE_TESTS.md), [OWNER_DECISIONS_MERGE_REPORT.md](OWNER_DECISIONS_MERGE_REPORT.md), [PRODUCT_REQUIREMENTS.md](PRODUCT_REQUIREMENTS.md), [PROJECT_AUDIT.md](PROJECT_AUDIT.md). Requirements/architecture/roadmap and worked cases now apply the16 answers; recovery only adds reservation/settlement/shift checks. DR-33–35 add **CONFIRMED code-traced** differences for restored resume release, absent refund settlement/rounding facts and unprotected report counts; original evidence remains historical. PROJECT_AUDIT.md changes **only nine BackupAdmin link targets**, no finding text or severity. Ten existing citations now point to [the tracked BackupAdmin view](../SwiftCheckoutPOS.UI/Views/BackupAdmin/Index.cshtml), lines1–119; [archived snapshot](EVIDENCE_REFERENCES.md) is marked **superseded — file is now tracked**. Generated NuGet provenance remains historical.

### Remaining questions — no answer invented

No direct contradiction found among section N answers. CQ-03 is read in its financial-view-OFF heading; PR-Q92 still governs ON financial read. CQ-12's specific Cashier expected/difference ban limits CQ-15's general own-shift exception. Four new edges are explicitly **OPEN**, separate from the16 answered CQs:

- **CQ-17** (CQ-02/05; PR-Q09): How should residual paisa be assigned when proportional allocations rounded to 2 decimals do not sum to the discount/refund? For example BDT1 across three equal lines yields 0.33 each (0.99). Which line/tender receives the remaining 0.01?
- **CQ-18** (CQ-05/10/16): For a Due sale later collected through another tender, does “original tenders actually paid” include those later collections? Example Cash20 + Due80, later Card40, unpaid40: on a full100 refund, should payout after due cancellation be Cash20/Card40? How should sale cash-rounding affect the refundable amount/payout cap?
- **CQ-19** (CQ-07/14/16; PR-Q36/39/40): Does the separate Cash rounding line increase/decrease Net Profit and the average-profit/capital-payback calculation, or is it disclosure only? Revenue/VAT exclusion and actual cash/till inclusion are answered; this profit aggregation is not explicit.
- **CQ-20** (CQ-04; PR-Q18): Does the configured 72-hour expiry keep running from original hold time after resume, reset, pause, or cease while resumed? Reservation retention is answered; the expiry clock for the resumed state is not.

Gates/done-evidence are linked per affected task; answered CQ-01–16 are not left as blockers. New CQ-17–20 only gate their stated edges. OAT-07/09/13/15/17/18/34–38 include derived cases marked for owner confirmation, not executed results.

### Ignore and scope verification

Read-only commands (Git root is the parent directory); no ignore rules changed:

```powershell
git --no-optional-locks -C C:/source/repos status --ignored --short --untracked-files=all -- SwiftCheckoutPOS
git --no-optional-locks -C C:/source/repos ls-files -- SwiftCheckoutPOS/SwiftCheckoutPOS.UI/Views/BackupAdmin
```

Exit0. Initial project status was clean; ls-files lists Index.cshtml. Status --ignored returned451 generated/build/editor entries. Exhaustive candidate enumeration includes bin/obj/.vs as well as authored files. NUL-delimited check-ignore avoids PowerShell CRLF being treated as part of a filename; an initial CRLF-delimited attempt was superseded and is not used as evidence. Exact successful enumeration/check command:

```powershell
$cqCandidates=@(rg --files --hidden --no-ignore -g '*.cs' -g '*.cshtml' -g '*.js' -g '*.css' -g '*.json' .); $cqPaths=@($cqCandidates | ForEach-Object { 'SwiftCheckoutPOS/'+($_ -replace '^[.][\\/]','').Replace('\','/') }); function Invoke-CqIgnore([bool]$cqNoIndex){ $cqStart=[Diagnostics.ProcessStartInfo]::new('git.exe'); $cqStart.UseShellExecute=$false; $cqStart.CreateNoWindow=$true; $cqStart.RedirectStandardInput=$true; $cqStart.RedirectStandardOutput=$true; $cqStart.RedirectStandardError=$true; foreach($cqArg in @('--no-optional-locks','-C','C:/source/repos','-c','core.quotePath=false','check-ignore','-z','-v','--stdin')){ $cqStart.ArgumentList.Add($cqArg) }; if($cqNoIndex){ $cqStart.ArgumentList.Add('--no-index') }; $cqProcess=[Diagnostics.Process]::new(); $cqProcess.StartInfo=$cqStart; [void]$cqProcess.Start(); $cqOut=$cqProcess.StandardOutput.ReadToEndAsync(); $cqErr=$cqProcess.StandardError.ReadToEndAsync(); $cqInput=[Text.Encoding]::UTF8.GetBytes(($cqPaths -join [char]0)+[char]0); $cqProcess.StandardInput.BaseStream.Write($cqInput,0,$cqInput.Length); $cqProcess.StandardInput.Close(); $cqProcess.WaitForExit(); $cqRaw=$cqOut.GetAwaiter().GetResult(); $cqError=$cqErr.GetAwaiter().GetResult(); $cqParts=$cqRaw.Split([char]0); $cqEntries=for($cqAt=0;$cqAt+3 -lt $cqParts.Length;$cqAt+=4){ [pscustomobject]@{Source=$cqParts[$cqAt];Line=$cqParts[$cqAt+1];Pattern=$cqParts[$cqAt+2];Path=$cqParts[$cqAt+3];Ignored=(-not $cqParts[$cqAt+2].StartsWith('!'))} }; [pscustomobject]@{ExitCode=$cqProcess.ExitCode;Error=$cqError;Entries=@($cqEntries)}; $cqProcess.Dispose() }; [pscustomobject]@{CandidateCount=$cqPaths.Count;CandidatePaths=$cqPaths;Default=(Invoke-CqIgnore $false);NoIndex=(Invoke-CqIgnore $true)} | ConvertTo-Json -Depth 6 -Compress
```

Result: **214** .cs/.cshtml/.js/.css/.json files checked, both check-ignore modes exit0; **56 ignored, all generated** under bin/obj/.vs, matching parent .gitignore lines33/34/39. **0 authored source/config files ignored**, including the tracked BackupAdmin view; --no-index also finds0 authored tracked files matched by positive ignore rules. No newly hidden authored source found. The451 status entries include binary/editor outputs outside those five extensions.

Final integrity checks compare all202 non-doc authored files and the entire OWNER_DECISIONS.md/parent AGENTS.md to pre-edit SHA256 baselines. All remain identical. Markdown link/path checks and CQ/register counts were validated; no app/build/test/database operation was performed. Runtime SQL/hardware/security/recovery outcomes remain unverified.

Final project status (`git --no-optional-locks -C C:/source/repos status --short --untracked-files=all -- SwiftCheckoutPOS`), docs only:

```text
 M SwiftCheckoutPOS/docs/ARCHITECTURE.md
 M SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md
 M SwiftCheckoutPOS/docs/DEVELOPMENT_ROADMAP.md
 M SwiftCheckoutPOS/docs/EVIDENCE_REFERENCES.md
 M SwiftCheckoutPOS/docs/OFFLINE_ACCEPTANCE_TESTS.md
 M SwiftCheckoutPOS/docs/OWNER_DECISIONS_MERGE_REPORT.md
 M SwiftCheckoutPOS/docs/PRODUCT_REQUIREMENTS.md
 M SwiftCheckoutPOS/docs/PROJECT_AUDIT.md
```
