# SwiftCheckoutPOS — Owner Decisions

Status: Answers to PRODUCT\_REQUIREMENTS.md section 6 (PR-Q01–PR-Q84).
Each answer below is APPROVED by the owner unless marked "PENDING".
Items marked ✏️ were filled in by the owner with shop-specific details.

## A. Money and rounding

* **PR-Q01 Currency:** Bangladeshi Taka (BDT, ৳) on all screens, receipts and stored values.
Always format as BDT, never by Windows regional settings.
* **PR-Q02 Cost price scale:** 2 decimal places (paisa). Weighted-average cost produces fractions; keep them.
* **PR-Q03 Selling price scale:** 2 decimal places allowed (most prices will be whole taka).
* **PR-Q04 Tax rate precision:** 2 decimal places (e.g. 7.50%).
* **PR-Q05 Rounding stages:** Never round stored prices when editing or displaying a product.
Round each line total to 2 decimals; calculate VAT for the whole bill, then round to 2 decimals;
round the final total to 2 decimals.
* **PR-Q06 Midpoint rule:** Round half away from zero (0.005 → 0.01), the normal commercial rule.
* **PR-Q07 Cash rounding:** ⚠️ CHECK — Round the final payable amount to the nearest whole taka
for ALL tenders. Show the difference as a "Rounding" line on the receipt and store it with the sale.

## B. VAT / tax

* **PR-Q08 Inclusive or exclusive:** ⚠️ CHECK — Selling prices are VAT-INCLUSIVE (shelf price = price
the customer pays). VAT is calculated out of the price for receipts and reports.
(Note: current code adds VAT on top. This answer changes checkout calculation.)
* **PR-Q09 Bill discount allocation:** Spread a bill discount across items in proportion to each
line's value (current behaviour), then calculate VAT on the discounted amounts.
* **PR-Q10 Tax rates:** Each product has its own VAT rate, entered by Manager/Admin.
0% for exempt items. ✏️ FILL IN: our standard VAT rate per our VAT registration is \_\_\_\_%.
No automatic tax rules beyond the product's rate.

## C. Discounts

* **PR-Q11 Who may discount:** Cashier, Manager and Admin, within the limits in PR-Q67.
* **PR-Q12 Flat + percentage together:** Not allowed. Only one bill discount at a time;
the screen must make the cashier choose one. Reject a request containing both.
* **PR-Q13 Item-level discounts:** Yes, required.
* **PR-Q67 Discount limits:** ⚠️ CHECK — Cashier: up to 5% of the bill or ৳100, whichever is lower.
Manager: up to 20%. Admin: no limit. Above the limit requires a Manager/Admin to log in and approve.
Every discount records who gave it.

## D. Stock and held orders

* **PR-Q14 Negative stock:** (Already answered 2026-10-31) Reject and ask staff to reduce quantity.
* **PR-Q15 Held orders reserve stock:** Yes. Held items cannot be sold to another customer.
* **PR-Q16 Valuation while held:** Held items are still shop stock. Inventory value and weighted-average
cost must include held quantities.
* **PR-Q17 Cancelling:** Cancelling an active or held order releases its reserved stock.
The held order is marked "Cancelled" and kept in history, never deleted.
* **PR-Q18 Held-order expiry:** Held orders expire after 72 hours (setting, Manager/Admin can change).
Expired orders are automatically cancelled (stock released) and kept in history.
0 hours means held orders never expire.
* **PR-Q19 Valuation method:** Moving weighted-average cost: yes.
* **PR-Q20 Zero-cost restock:** Allowed only for free/bonus stock, with a required reason.
Stock increases and average cost goes down accordingly; no ledger cash entry.
* **PR-Q21 Batch cost contents:** The total amount actually paid for the batch, including transport
if staff choose to include it. No automatic allocation.
* **PR-Q22 Selling below cost:** Blocked by default. Manager/Admin may override with a reason
(e.g. clearance). Override is recorded.
* **PR-Q23 Who corrects stock counts:** Manager and Admin only.
* **PR-Q24 Financial posting for corrections/damage:** Stock decrease → "Inventory Adjustment" (or
"Damaged") expense at current average cost. Stock increase (found stock) → "Inventory Adjustment"
gain at current average cost.
* **PR-Q25 Vendor return without cash:** Reduce stock; reduce inventory value at current average cost;
record a note "vendor credit pending". No cash ledger entry until money is actually received.
* **PR-Q77 Deactivated product in cart:** Block completion and ask the cashier to remove the item.
* **PR-Q82 Stock correction reason:** Required, chosen from: Count correction, Damaged, Expired,
Theft/Loss, Found, Other (with a note). Records who and when.

## E. Refunds

* **PR-Q26 Partial refunds:** ⚠️ CHECK — Yes, by item and quantity. Version 1 may ship with full-sale
refunds only; partial refunds follow as the next enhancement.
* **PR-Q27 Eligibility:** Completed sales only, within 7 days, with the receipt number.
Requires Manager or Admin approval. Cannot refund more than was sold.
* **PR-Q28 Refund tender:** Refund using the original payment method. Cash refunds come from the drawer.
* **PR-Q29 Returned goods:** Staff choose per item: "Return to stock" (default) or "Damaged".
* **PR-Q30 VAT on refunds:** Reverse the VAT of the refunded items in proportion.
* **PR-Q31 Reporting period:** A refund appears on the date the refund happened.
The original sale is never changed or moved.
* **PR-Q65 COGS on refunds:** Items returned to stock reverse COGS at their original cost-at-sale.
Damaged items are recorded as "Damaged" expense instead.
* **PR-Q66 Refund ledger entries:** Record the refund amount (money out), the VAT reversal, and the
COGS reversal (if restocked), all linked to the refund record and original sale.

## F. Customers and loyalty

* **PR-Q32 Loyalty earning:** (Interim answer 2026-10-31) Link the customer; no points for now.
* **PR-Q70 Loyalty redemption:** Not used for now (same as PR-Q32).
* **PR-Q71 Loyalty reversal:** Not applicable until a loyalty rule is defined.

## G. Finance and reports

* **PR-Q33 Manual ledger categories:**
Inflow: Capital Investment, Loan Received, Other Income.
Outflow: Rent, Utilities, Salaries, Transport, Maintenance, Other Expense, Capital Withdrawal,
Loan Repayment.
"Inventory", "COGS", "Inventory Adjustment" and "Damaged" are system-only (not manual).
* **PR-Q34 Net balance meaning:** Rename to "Net Cash Position" = (all money in: sales collected +
all ledger inflows) − (all money out: refunds paid + all ledger outflows).
* **PR-Q35 Operating expenses:** Only expense categories (Rent, Utilities, Salaries, Transport,
Maintenance, Other Expense). Capital Withdrawal and Loan Repayment are NOT operating expenses,
in every view.
* **PR-Q36 VAT in revenue/profit:** Revenue and profit exclude VAT everywhere.
* **PR-Q37 Historical profit:** Use cost at the time of sale, never today's cost.
* **PR-Q38 Old sales without cost-at-sale:** Do not invent a cost. Show "cost unknown" for those
sales and exclude them from profit totals, with a visible note.
* **PR-Q39 Break-even meaning:** Capital payback. Rename the card "Capital payback estimate".
* **PR-Q40 Business start date:** The earlier of the first capital entry and the first sale.
* **PR-Q58 Business day:** Midnight to midnight, local time.
* **PR-Q59 Search and KPI totals:** No. KPI cards follow the date range only; search filters the table only.
* **PR-Q79 Time zone:** Asia/Dhaka (UTC+6) for all business dates and reports.
* **PR-Q80 Lifetime cards:** Ignore the selected date range and are clearly labelled "All time".
* **PR-Q81 Months for averages:** Elapsed days ÷ 30.44, minimum 1 month.

## H. Till and cash drawer

* **PR-Q41 Opening float:** Entered by the cashier when opening a shift (no hard-coded amount).
* **PR-Q42 Shift boundary:** Explicit "Open shift" and "Close shift" by a user. Only one shift open at a time.
* **PR-Q43 Expected cash:** Opening float + cash sales − cash refunds + cash paid in − cash paid out,
during that shift only.
* **PR-Q44 Blind count:** Yes. The cashier enters the counted amount before seeing expected cash.
Manager/Admin can see both afterwards.
* **PR-Q47 When the drawer opens:** Cash sales and cash refunds only. A manual "No sale" opening is
allowed for Manager/Admin with a reason, and is logged.

## I. Receipts and payments

* **PR-Q45 Receipt numbers:** Assigned only when the sale completes. Format: SCP-YYYYMMDD-0001,
sequential per day, never reused.
* **PR-Q46 Card / mobile wallet:** No payment integration. The cashier confirms the payment was received
on the separate terminal/phone and may enter a reference (last 4 digits, or bKash/Nagad TrxID).
* **PR-Q48 Receipt content:** ✏️ FILL IN shop name, address, phone and BIN/VAT number.
Also: receipt number, date/time of sale, cashier name, items (qty, price, line total), discount,
VAT, rounding, total, payment method, amount paid, change, footer message. Values always come from
the saved sale.
* **PR-Q83 Reprints:** Marked "REPRINT / COPY", showing reprint date/time and who reprinted.
Original values unchanged. Every reprint is logged.

## J. Staff, login and permissions

* **PR-Q49 Transferring held orders:** Any logged-in staff member may resume a held order.
Record who held it and who resumed it.
* **PR-Q50 Permission matrix:** ⚠️ CHECK
Cashier: checkout, hold/resume, customer lookup/add, open/close own shift and till count.
Manager: everything a Cashier can do + inventory, customers, refunds, reports, till reconciliation,
finance entries, settings.
Admin: everything a Manager can do + user management, backup/restore, system configuration.
Admin includes all Manager permissions. Backup download is Admin-only.
* **PR-Q51 Inactivity lock:** After 15 minutes without activity, lock the screen; the same user (or a
Manager/Admin) re-enters a password. The cart is preserved.
* **PR-Q52 First Admin:** Created by the owner during installation using a one-time setup step on
the shop PC. No default password.
* **PR-Q62 Account recovery:** An Admin can reset another user's password. Raise password hashing
strength to the current recommendation if login still takes under 1 second on the shop PC.
* **PR-Q64 Login expiry:** 12 hours maximum, plus the 15-minute inactivity lock.
* **PR-Q68 Emergency Admin recovery:** Owner only, using a local maintenance step on the shop PC.
* **PR-Q78 Failed logins:** After 5 failed attempts, wait 30 seconds, increasing up to 5 minutes.
No permanent lockout. Failed attempts are logged.

## K. Hardware and media

* **PR-Q60 Receipt printer:** ✏️ FILL IN model: unknown — decide later. Expected: 80mm thermal, ESC/POS, USB
(or USB virtual COM port). Until then, virtual printer stays clearly labelled "Virtual".
* **PR-Q75 Barcode scanner:** ✏️ FILL IN model: unknown — decide later. Mode: USB keyboard-wedge (HID).
Serial scanner not required.
* **PR-Q76 Cash drawer:** ✏️ FILL IN model: unknown — decide later. Connected to the receipt printer (RJ11),
opened by the printer's ESC/POS drawer pulse.
* **PR-Q61 Barcode rules:** Letters, digits and hyphen only; 4–32 characters; no spaces or symbols.
* **PR-Q72 Image formats:** JPG, PNG, WebP.
* **PR-Q73 Image file size:** Maximum 2 MB.
* **PR-Q74 Image dimensions:** Maximum 2000 × 2000 pixels.

## L. Backup and recovery

* **PR-Q53 Backup frequency:** Automatic daily backup at closing time, with catch-up if the PC was
off. Manual backup before every upgrade.
* **PR-Q54 Recovery time (RTO):** The shop must be selling again within 2 hours of a failure.
* **PR-Q55 Retention:** Keep the last 30 daily backups and the last 12 monthly backups
(first backup of each month). Never delete the newest verified backup.
* **PR-Q56 Separate copy:** ✏️ FILL IN — Copy to a USB drive kept away from the PC, at least
every \_\_\_\_ days (recommended: daily, minimum weekly).
* **PR-Q57 Files to keep with backups:** Product images and settings files. Virtual receipt
files are not needed (receipts can be reprinted from the database).
* **PR-Q63 Capacity:** ✏️ FILL IN — About \_\_\_\_ sales per day and \_\_\_\_ products.
* **PR-Q69 Maximum data loss (RPO):** At most one business day of sales.
* **PR-Q84 Restore drill:** Test-restore the latest backup into a test database once a month and
before every upgrade.

## Approval

Approved by: \_\_\_\_\_\_\_\_\_\_\_\_  Date: \_\_\_\_\_\_\_\_\_\_\_\_

