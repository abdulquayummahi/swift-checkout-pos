# SwiftCheckoutPOS Product Requirements

Status: requirements baseline for future work · 2026-10-31. No source/schema/package/configuration change or test execution is authorized by this document.

Implementation update: the owner subsequently authorized checkout reconstruction. See [CHECKOUT\_RESTORATION.md](C:/source/repos/SwiftCheckoutPOS/docs/CHECKOUT_RESTORATION.md) for the restored code, regression evidence and remaining SQL/runtime gates. The audit and original baseline statements below describe the pre-restoration source.

## 1\. Product and fixed scope

SwiftCheckoutPOS is a standalone offline-first POS on **one Windows computer**, locally hosted with the existing **ASP.NET Core MVC/.NET 10** solution, **local SQL Server**, locally connected POS hardware and local records/configuration/logging/backups. Internet is not required for any core function. It must preserve committed business records and recoverable active/held work across process/Windows restarts and approved upgrades.

Retain MVC/UI/BLL/DAL and existing features. Multi-terminal/network databases, branches, cloud hosting/storage/identity, synchronization, remote access, microservices and message brokers are outside scope. No SQLite migration or architecture rebuild is proposed.

The actual source/audit baseline, SQL edition evaluation and target architecture are in [ARCHITECTURE.md](C:/source/repos/SwiftCheckoutPOS/docs/ARCHITECTURE.md); detailed evidence/VERIFIED-CONFIRMED-SUSPECTED findings remain in [PROJECT\_AUDIT.md](C:/source/repos/SwiftCheckoutPOS/docs/PROJECT_AUDIT.md). No current feature is certified Works by this requirements document. Missing CheckoutService/build/session/schema blockers remain.

## 2\. Users and current role policy

Users are local employee accounts: Cashier, Manager and Admin. Initial Admin/maintenance is controlled offline, never a public default password. Windows/SQL maintenance identity is distinct from ordinary staff application roles.

|Role / capability|Current source gate to preserve until PR-Q50 is answered|
|-|-|
|Anonymous|Account login/access-denied and Home/error views; no POS/admin data|
|Cashier|Authenticated Checkout actions, cart/customer association/tender flow; cannot type an Admin/Manager URL to gain access|
|Manager|Checkout; Inventory, Customer, Finance, Backup, Refund, Reconciliation, ReportAdmin and Settings|
|Admin|Those shared routes plus UserAdmin, AdminDashboard, SalesAdmin and legacy Report routes|
|Manager-only dashboard|Current route excludes Admin; do not assume role hierarchy|
|Local maintenance operator|Windows/SQL service/config/certificate/backup/restore/upgrade duties, under explicit local authorization; not an automatic privilege of a cashier|

Reference: audit 11.3 and source authorization attributes. Stale cookie privileges (PC-01), missing Manager options (PC-06), fixed till actor (PC-08) and absent first-Admin setup (PC-09) are defects, not desired behavior. Account disable/demotion/password reset must revoke existing access; every mutation is authorized and antiforgery-protected.

## 3\. Feature requirements and known implementation state

|ID|Feature requirement|Current source status / audit evidence|
|-|-|-|
|FR-01|Local authentication, secure password storage, user create/edit/disable/roles and attributable actions|Local PBKDF2/cookies exist; PC-01/05/06/07/09 and AUD-02 block readiness|
|FR-02|Product/inventory create/edit/search, prices/tax fields, stock display, local media and safe explicit adjustments|Implemented paths; precision, stale stock and media defects PB-01/PB-04/PC-03|
|FR-03|Categories/vendors remain locally maintainable from existing workflows|Quick-add currently fails token wiring AUD-23; no feature removed|
|FR-04|Barcode/manual item entry, cart quantities/discounts/customer/tender selection and exactly-once completed sale|UI/DAL exist; missing CheckoutService AUD-01 prevents full validation/persistence trace|
|FR-05|Durable active/held orders and safe resume/cancel/expiry according to approved rules|Existing hold/resume integration incomplete; potential durable-to-session loss PB-18, valuation risk PB-23|
|FR-06|Local customer enrollment/edit/history and approved loyalty behavior|Local lookup/profile paths; writer/loyalty policy incomplete; no online identity|
|FR-07|Receipt-based refunds with eligibility, stock/tender/ledger/customer effects approved by owner|Current full-sale-only path rejects Held/repeated refunds; retry/audit gaps PB-02/PB-09|
|FR-08|Manual/system financial ledger with validated categories/types, atomic related stock/sale effects and truthful outcomes|PB-03/PB-06/PB-07/PB-08; formulas awaiting policy|
|FR-09|Reliable completion-cost facts and approved COGS/gross/net indicators|CostAtSale field exists but supplied writer/migration absent; historical readers use current cost PB-05|
|FR-10|Dashboard, date-range/customer reports and Z-report using agreed status/date/financial definitions|Source exists, placeholders remain; formulas documented in audit section 8, not approved as policy|
|FR-11|Till counting/reconciliation with real actor and one approved shift/cash definition|Count binding, differing formulas and actor defects AUD-05/AUD-07/PC-08|
|FR-12|Authorized versioned shop settings and validated machine/hardware configuration|Held retention exists; migration/consumer missing; unsafe defaults/old backup config migration required|
|FR-13|Local manual/automatic backups with verified outcome, visible failure/overdue state and recovery procedure|Native backup exists; PB-11–15/PB-21–22; sole normative procedure [DATA\_RECOVERY\_PLAN.md](C:/source/repos/SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md)|
|FR-14|Local receipt preview/physical printing/drawer with safe reprint from saved sale; hardware failure never rewrites committed money/stock|Virtual selected; physical receipt incomplete AUD-22, token/race AUD-23/PB-19|
|FR-15|All prior features work without internet using bundled assets/local SQL/local auth/local hardware|No required static external dependency found; dynamic stored image paths PC-14 need verification|
|FR-16|Start after Windows boot; survive crash/power loss and compatible upgrades without silently losing/rebuilding records|Target Windows Service/durable SQL order/outcome model; current production deployment unverified PC-10/PC-13|

## 4\. Nonfunctional and engineering requirements

|ID|Requirement|
|-|-|
|NFR-01|One Windows host/origin; enforce loopback-only HTTP(S) and local SQL; no remote API/identity/config/license dependency for core operation|
|NFR-02|Preserve SQL financial facts/master references, media and required configuration across restart/upgrade; durable drafts/operation results, not Session alone|
|NFR-03|Save each required sale/refund/stock/ledger/audit unit atomically inside EF execution strategy, keeping EnableRetryOnFailure|
|NFR-04|Stable operation IDs, uniqueness and outcome lookup make double-clicks/replays/unknown commits safe; stale row versions produce reviewable conflicts|
|NFR-05|BLL authoritative validation/calculation; decimal money; DTO/ViewModel boundary with no entities in Razor; server roles/tokens cannot be bypassed by UI manipulation|
|NFR-06|Local staff security: account revocation, least-privilege OS/SQL/files, safe media/download paths, generic unexpected errors and redacted local rolling logs|
|NFR-07|No logging/printing failure can turn a committed sale into a failed/repeated financial transaction; no side effects inside DB retry delegates|
|NFR-08|No automatic startup schema migration/recreation; reviewed SQL and verified pre-change recovery for existing databases|
|NFR-09|Retain all business records; use bounded queries/approved aggregates rather than deleting history to improve performance or fit Express capacity|
|NFR-10|Dedicated test SQL catalog only; test create/reset/backup/restore can never target the shop DB/files/keys/hardware|
|NFR-11|Document recoverability with actual isolated restore/reboot/crash/offline evidence; Step 5 executes cases, this step records requirements only|

Performance targets, capacity and recovery objectives must be measured/approved rather than invented. Local diagnostic retention defaults are technical, never permission to remove financial/audit history.

## 5\. Business behavior known from source, pending owner approval

The following are **observed behavior, not newly selected policies**. Full formulas and paths are in audit 8–9:

* Product create/edit reject selling below cost, but whole-round prices/tax; restock stores two-decimal WAC and optional retail. This inconsistency is a confirmed defect; correct precision/stages are PR-Q02–07.
* Restock requires positive added quantity and nonnegative batch cost; vendor return checks sufficient quantity and records cash only when refundAmount>0. Zero-cost/no-cash/value policies remain open.
* Cart line total is R2(max(0, UnitPrice-ItemDiscount) × max(0,Quantity)). Bill flat discount takes precedence over percentage, is capped to gross subtotal, and tax is proportionally allocated/added. These formulas must not become accepted policy merely by writing tests for current code.
* Park deducts stock; resume restores quantities and deletes held rows in DAL. Actual BLL and ownership/expiry rules are unavailable. Target technical durability preserves records; reservation semantics still require owner answers.
* Current refunds are full-sale only, reject Held/already Refunded and restore stored quantities. No partial-refund requirement, payout method, damage disposition or financial reversal rule is invented.
* Manual finance uses positive Amount and Inflow/Outflow strings; reserved-category normalization/type defects must be fixed. Current NetLedgerBalance is capital inflows minus inventory/damage outflows and selected expenses, not all inflows minus all outflows.
* Report profit uses present product cost; Finance lifetime profit uses TotalAmount minus COGS-ledger outflows minus selected operating outflows. Completion-cost consistency is required, but approved indicators/tax/category definitions remain open.
* The “break-even” formula estimates remaining capital/positive average profit; opening float and shift definitions differ by path. Do not label those calculations accounting policy without approval.
* Existing Cash/Card/Mobile Wallet are labels; no bank/payment-provider integration was found. Required offline operator confirmation is unresolved.
* Current local auth has three roles and a last-active-admin guard, but issued-cookie revocation and concurrent guard correctness need correction/testing; password/role values are not accepted from unauthenticated UI requests.

## 6\. OPEN QUESTIONS

Each numbered item asks one unknown rule/requirement and notes current behavior for reference. **Answers are not implied by defaults, code comments or this architecture.** Financial/stock/tax/refund questions affecting a workflow are release gates for that workflow. Record the owner’s answer, decision date and approved examples beside the ID; implementation/tasks/tests must link those IDs.

|Question ID|One owner decision required|Current behavior / evidence reference|Area|
|-|-|-|-|
|PR-Q01|Which currency must all screens, receipts and stored monetary values represent?|Receipt text uses BDT; some views use host-culture C formatting. Audit OQ-02; VirtualPrinterService:109–124 and TillCountSummary:24–34.|Money|
|PR-Q02|What scale is allowed for product cost prices?|InventoryService whole-rounds create/edit/list costs; restock uses two decimals; SQL decimal(18,2). PB-01 / InventoryService:37,96,125,152,172.|Money|
|PR-Q03|What scale is allowed for selling prices?|Whole-rounded mappings/forms conflict with two-decimal restock retail and storage. PB-01 / InventoryService:38,97,126,152,180.|Money|
|PR-Q04|What fractional precision is allowed for tax rates?|Rates are Math.Round(decimal) whole-rounded despite SQL decimal(5,2). InventoryService:43,98,120,152.|Tax|
|PR-Q05|At which calculation stages must monetary rounding occur?|Cart rounds line totals, aggregate discount, aggregate tax and final total to 2; product mappings round earlier. Audit 8.1–8.2.|Money|
|PR-Q06|Which midpoint rounding rule is required?|All Math.Round overloads default to ToEven; no explicit owner policy. Audit 8.1.|Money|
|PR-Q07|Is any separate cash-tender rounding adjustment required?|No explicit cash rounding adjustment is supplied; checkout/receipt displays often N0. Audit 8.1.|Money|
|PR-Q08|Are selling prices inclusive or exclusive of tax?|CheckoutSessionDTO computes discounted taxable amounts then adds TaxAmount to NetSubTotal. Audit 8.2.|Tax|
|PR-Q09|How must a bill discount be allocated for tax across different item rates?|DTO allocates discount proportionally to line totals, clamps rate to 0–100 and rounds aggregate tax. Audit 8.2.|Tax|
|PR-Q10|What tax eligibility, exemption or rate-selection rules must apply?|Product TaxRate is the supplied basis; no documented tax/VAT jurisdiction policy. Audit OQ-03.|Tax|
|PR-Q11|Which staff roles may apply discounts?|Authenticated Checkout exposes ApplyBillDiscount; final BLL is missing, so authority/limits cannot be traced. CheckoutController:149–164; PB-16.|Discount|
|PR-Q12|How should simultaneous flat and percentage bill discounts be resolved?|DTO gives positive flat amount precedence, otherwise percentage, and caps at gross subtotal. Audit 8.2.|Discount|
|PR-Q13|Are item-level discounts part of the required workflow?|CartItemDTO subtracts per-unit ItemDiscount before line-total rounding; complete checkout writer is absent. Audit 8.2.|Discount|
|PR-Q14|May available stock become negative or be sold as a backorder?|**ANSWERED by owner, 2026-10-31:** reject checkout and ask staff to reduce quantity. Restored CheckoutService checks availability when adding and inside completion/hold transactions. Historical lead PB-16 was SUSPECTED.|Inventory|
|PR-Q15|Do held orders reserve available stock?|ParkHeldOrder subtracts stock; resume adds it back. BLL orchestration is absent. SalesRepository:65–110; PB-23.|Inventory|
|PR-Q16|Which quantity must inventory valuation include while stock is held?|Restock WAC uses max(0, StockQuantity); held deductions may exclude still-owned goods. PB-23.|Inventory|
|PR-Q17|What should cancellation of an active or held order do to reservations and history?|ClearCart removes session; resume deletes held SQL rows in available DAL. Durable cancellation policy not supplied. PB-18.|Inventory|
|PR-Q18|What does held-order retention/expiry mean, including zero hours?|Setting defaults to 72 hours and allows 0–87600; repository date filter hides older held rows without releasing stock. Missing consumer. Audit OQ-05.|Inventory|
|PR-Q19|Is moving weighted-average cost the intended valuation method?|Restock computes R2((max(0,q)×cost + batchCost)/(max(0,q)+addedQty)); sale-time cost writer is missing. Audit 9.4.|Inventory|
|PR-Q20|How should a valid zero-cost restock be treated financially?|BLL allows batchCost=0 and posts Inventory outflow of that amount; malformed input also incorrectly becomes 0 (PB-08).|Inventory|
|PR-Q21|What acquisition costs belong in the restock batch cost?|One submitted totalBatchCost drives WAC; no documented freight/rebate/allocation policy. InventoryService:160–186.|Inventory|
|PR-Q22|Is selling below the updated cost ever allowed?|Create/edit reject selling<cost; restock checks only an explicitly supplied positive new retail price. Audit OQ-09.|Inventory|
|PR-Q23|Which staff roles may perform a stock count correction?|Edit currently assigns an absolute posted StockQuantity with no separate movement reason. PB-04 / InventoryService:152.|Inventory|
|PR-Q24|What financial posting should a stock correction or damaged stock create?|Direct edit posts no ledger adjustment; Damaged is a manual finance category. Audit OQ-06/OQ-12.|Inventory|
|PR-Q25|How should a vendor return with no immediate cash refund affect inventory value and finance?|Quantity is reduced; ledger records only refundAmount>0, without revaluing remaining unit cost. InventoryService:190–203.|Inventory|
|PR-Q26|Are partial customer refunds required?|Only full-sale refund by receipt is supplied; no posted quantity/amount exists. RefundService:48–62; PB-09.|Refund|
|PR-Q27|What conditions make a completed sale eligible for a refund?|BLL rejects missing, Refunded and Held; no documented time/reason/other eligibility rule. Audit 9.1–9.2.|Refund|
|PR-Q28|Which tender should pay a customer refund?|Current refund has no separate refund-tender record or payout integration. PB-09.|Refund|
|PR-Q29|Should returned customer goods always re-enter sellable stock?|Current full refund restores every stored quantity without a damage/disposition choice. SalesRepository:169–175.|Refund|
|PR-Q30|How should a refund reverse tax?|Current path only changes sale status/stock and does not post ledger events. PB-09; no reversal policy inferred.|Refund|
|PR-Q31|Which reporting period should show a later refund?|Reports filter original SaleDate after status is changed; no independent refund timestamp. Audit 8.5.|Reporting|
|PR-Q32|What loyalty earning rule is required?|**INTERIM ANSWER by owner, 2026-10-31:** link the customer but leave points unchanged until a rule is defined. Restored checkout does not earn or redeem points. A future formula and refund reversal remain open.|Customer|
|PR-Q33|Which manual ledger category/type combinations are allowed?|UI offers Capital/Loan/Misc and several expenses; BLL only attempts to reserve Inventory/COGS and coerces unknown type. PB-06/PB-07.|Finance|
|PR-Q34|What should NetLedgerBalance represent?|Current C - InventoryOutflow - selectedOpex excludes Loan/Misc/vendor-refund inflows and COGS. Audit 8.3.|Finance|
|PR-Q35|What should count as operating expense consistently across period and lifetime views?|Capital withdrawal is included in period Opex but excluded from lifetime Opex. FinanceService:54–82.|Finance|
|PR-Q36|Should reported revenue/profit exclude tax collected for third parties?|Finance lifetime revenue sums TotalAmount; report estimated profit subtracts TaxAmount algebraically. Audit 8.4–8.5.|Finance|
|PR-Q37|Is the desired historical profit a completion-cost gross margin or a current replacement-cost estimate?|Readers use current Product.CostPrice despite CostAtSale’s stated intent. PB-05.|Finance|
|PR-Q38|What reliable historical evidence can populate missing sale-time cost values?|CostAtSale field exists without supplied migration/writer; current price is not historical proof. PB-05/AUD-03.|Finance|
|PR-Q39|Should the break-even display mean operating break-even or capital payback?|Code projects remaining capital/positive average monthly profit, not contribution-margin units. Audit 8.4.|Finance|
|PR-Q40|What event establishes the business start date for averages?|Code prioritizes first Capital entry over first sale, uses month-number difference and minimum 1. Audit 8.4.|Finance|
|PR-Q41|What opening float should the till use?|One service hardcodes 5,000 BDT; other path has none. AUD-07 / audit OQ-25.|Till|
|PR-Q42|What defines a shift boundary?|One path starts at latest CloseTime/today; another uses lifetime cash with a fabricated eight-hour OpenTime. AUD-07.|Till|
|PR-Q43|Which cash movements and sale/refund statuses belong in expected till cash?|Current-shift path includes all Cash statuses; blind path includes lifetime Completed cash only. Audit 16.3.|Till|
|PR-Q44|Must till counting remain blind until the count is committed?|One route previews expected cash; other intentionally reveals after submission. Audit 16.3.|Till|
|PR-Q45|When should receipt numbers be assigned and what sequence/format constraints apply?|Cart generates UTC-second plus six Guid hex characters before completion; receipt DB index is unique. CheckoutSessionDTO:60–61.|Receipt|
|PR-Q46|What should Card and Mobile Wallet labels require as offline operator confirmation?|Only PaymentMethod string exists; no supplied external payment authorization/API integration. Audit 9.3.|Tender|
|PR-Q47|For which tenders or actions should the cash drawer open?|Current print action always opens drawer without tender policy; formatter can also kick drawer. Audit 15.1–15.2.|Hardware|
|PR-Q48|What content must an original receipt retain?|Virtual receipts use current print date, literal Terminal 01 cashier and N0 totals; committed-sale reprint absent. Audit 15.1.|Receipt|
|PR-Q49|May active/held work be transferred between employees at the shared terminal?|One ActiveCart session key; logout clears session; no durable ownership/handover policy. PC-02/PB-18.|Access|
|PR-Q50|Is the existing route-role matrix the intended staff permission policy?|Admin/Manager gates as audit 11.3; Manager-only dashboard excludes Admin; Manager can access full backups/settings/finance. PC-06.|Access|
|PR-Q51|What inactivity lock rule should apply at the shared terminal?|Auth persistent 8-hour non-sliding ticket; session 8-hour idle timeout; not the same lifetime. Audit 11.4.|Access|
|PR-Q52|Who may perform initial Admin provisioning?|No safe first-Admin bootstrap supplied; normal creation requires Admin. PC-09.|Access|
|PR-Q53|What automatic backup frequency is required?|Default auto backup disabled at 23:00; missed runs have no catch-up. PB-13/PB-14. Architecture now requires catch-up when enabled; frequency remains unknown.|Recovery|
|PR-Q54|What maximum recovery time (RTO) is required?|No tested restore workflow or measured recovery time. PB-15.|Recovery|
|PR-Q55|How many verified backup generations must be retained?|No cleanup/retention implemented; timestamp names may collide. PB-22.|Recovery|
|PR-Q56|Which separate local/removable media must hold a verified recovery copy?|Executable-relative Backups default can share installation/disk failure. PB-21.|Recovery|
|PR-Q57|Which media/settings/receipt files are business records requiring long-term retention?|SQL .bak excludes images, backup\_config.json and virtual receipts. Audit 13.4.|Recovery|
|PR-Q58|What business-day boundary must reports use?|Now/Today and SQL getdate coexist with UTC invoice IDs; inclusive end ticks and Today-7 dashboards. PB-20 / audit 8.6.|Reporting|
|PR-Q59|Should search filters affect displayed KPI totals?|Period KPIs follow in-memory search; lifetime metrics load independent full history. Audit 8.3/8.6.|Reporting|
|PR-Q60|Which exact receipt printer model and connection mode must be supported?|Virtual mode selected; serial receipt empty, COM1/9600 code plus COM3 stub; HID scanner heuristic. AUD-22.|Hardware|
|PR-Q61|What barcode character and length rules are required?|Barcode Required-only feeds filename/wildcard cleanup; image extension rewritten without decoding. PC-03/PC-11.|Media|
|PR-Q62|What local account-recovery limits suit staff operation?|No application throttling/counters; 210,000 PBKDF2-SHA256 versus current external benchmark. PC-07/PC-12. Algorithm remains versioned PBKDF2.|Access|
|PR-Q63|What retained-record volume/growth must the production SQL edition support?|Database size was not read; new baseline Express 2022 has 10 GB cap. Existing edition/version unverified.|Capacity|
|PR-Q64|What authentication-ticket expiry is required?|Existing persistent cookie is 8 hours and non-sliding; Session is independently idle-expiring. Audit 11.4.|Access|
|PR-Q65|How should a refund reverse COGS?|No independent refund cost/ledger event is posted by current full-refund path. PB-09.|Refund|
|PR-Q66|Which ledger postings must a customer refund create?|Current refund changes sale status and stock only; required financial entries are undocumented. PB-09.|Refund|
|PR-Q67|What discount amount or percentage limits apply to each authorized role?|Final CheckoutService is missing, so limits cannot be traced. CheckoutController:149–164; PB-16.|Discount|
|PR-Q68|Who may authorize emergency recovery of an Admin account?|No controlled first-Admin/recovery mechanism is supplied; do not introduce a shared default credential. PC-09.|Access|
|PR-Q69|What maximum data age may be lost after a disaster (RPO)?|No verified restore/RPO evidence; backup frequency alone is not a proven recovery point. PB-15.|Recovery|
|PR-Q70|What loyalty redemption rule is required?|Customer fields exist, but checkout writer and redemption rules are unavailable. PB-16.|Customer|
|PR-Q71|How should a refund reverse customer loyalty changes?|Current RefundService does not invoke CustomerService. PB-09.|Customer|
|PR-Q72|Which uploaded image formats are permitted?|Current extension rewriting does not decode/verify image content. PC-11.|Media|
|PR-Q73|What maximum uploaded image file size is permitted?|No explicit per-image application size rule was found. PC-11.|Media|
|PR-Q74|What uploaded image dimension limits are permitted?|No explicit decoded width/height/pixel limit was found. PC-11.|Media|
|PR-Q75|Which barcode scanner model and keyboard-wedge or serial mode must be supported?|Keyboard-wedge heuristics exist; serial scanner implementation is not complete. Audit 15.|Hardware|
|PR-Q76|Which cash drawer model and trigger protocol must be supported?|Print action/formatter can trigger drawer, but device inventory is unavailable. Audit 15.|Hardware|
|PR-Q77|What must checkout do if a product already in a cart has since been deactivated?|Catalog reads filter active products; missing checkout writer prevents proving commit-time behavior. PB-16.|Inventory|
|PR-Q78|What failed-login attempt and temporary-delay limits suit the shared terminal?|No application throttle/counter exists. PC-07; avoid a permanent easy-to-trigger lockout policy.|Access|
|PR-Q79|Which business time zone must reports use?|Local Now/Today, SQL getdate and UTC receipt identifiers coexist. Audit 8.6.|Reporting|
|PR-Q80|Should lifetime cards ignore the selected reporting date range?|Current lifetime metrics independently retrieve full history. Audit 8.3/8.6.|Reporting|
|PR-Q81|How must elapsed months be counted for financial averages?|Current month-number difference is clamped to at least one month. Audit 8.4.|Finance|
|PR-Q82|What reason or evidence must accompany a stock count correction?|Product edit currently overwrites absolute StockQuantity without a dedicated movement reason. PB-04.|Inventory|
|PR-Q83|How must a reprinted receipt be labeled and attributed?|Committed-sale reprint is absent; virtual output has current print date and a literal cashier label. Audit 15.1.|Receipt|
|PR-Q84|What routine restore-drill cadence is required?|No restore implementation or drill evidence exists. Architecture requires qualification before production and after material recovery changes; routine cadence is undecided. PB-15.|Recovery|

## 7\. Acceptance and change control

Implementation work is tracked by [DEVELOPMENT\_ROADMAP.md](C:/source/repos/SwiftCheckoutPOS/docs/DEVELOPMENT_ROADMAP.md). Owners approve policy examples; engineers prove structural/security/recovery invariants on the dedicated test environment. [OFFLINE\_ACCEPTANCE\_TESTS.md](C:/source/repos/SwiftCheckoutPOS/docs/OFFLINE_ACCEPTANCE_TESTS.md) contains cases only for Step 5. [DATA\_RECOVERY\_PLAN.md](C:/source/repos/SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md) is the single backup/restore/crash/upgrade recovery document. A documentation update is not authorization to run production SQL, change packages or deploy a new binary.

