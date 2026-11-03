# Feature Gap Report (playbook Step 7)

Date: 2026-11-04. Compares [OWNER_DECISIONS.md](OWNER_DECISIONS.md) (PR-Q01–93, CQ-01–20) and the roadmap with the code at the
end of the branch chain. All 37 DR rows in [PRODUCT_REQUIREMENTS.md](PRODUCT_REQUIREMENTS.md) are marked FIXED.

## 1. Done (summary)

| Area | Owner rules | Roadmap |
| --- | --- | --- |
| Money, VAT-inclusive 0 %, cash whole-taka rounding | PR-Q01–10, 91, CQ-01/16/17 | IMM-05, BP-02 |
| Checkout, holds/reservations/expiry, receipt numbers | PR-Q11–18, 45, CQ-04/13/20 | BP-03, BP-16/17 |
| Discounts with role limits and approval | PR-Q12, 67, 90, 93 | BP-20 |
| Tenders (Cash/Card/bKash/Nagad/Due, max two), Due ৳2,500 | PR-Q85, 86, CQ-05/10/18 | BP-16/17 |
| Refunds (7 days, partial, damaged, Due-first), void ≤ 5 min | PR-Q26–31, 65, 66, 87, CQ-05/06/11/19 | BP-04/18 |
| Inventory: WAC, restock, zero-cost, vendor return, stock correction | PR-Q19–25, 82 | IMM-06/07, BP-14 |
| Shifts: one open, blind close, ৳1,000 approval, No sale | PR-Q41–44, 47, 88, CQ-08/12 | BP-19 |
| Finance: metric definitions, Opex categories, payback | PR-Q33–40, 58, 59, 80, 81, CQ-14 | T14 |
| Financial privacy (Admin toggle) | PR-Q50, 92, CQ-03/10/12/15 | BP-15 |
| Security: roles, 15-min lock, 12-h login, throttle, revocation, first Admin, `--reset-admin` | PR-Q49–52, 62, 64, 68, 78, CQ-09 | BP-21/22/13, IMM-09, BP-07 |
| Receipts: Admin header, saved-sale reprint REPRINT/COPY, ESC/POS + drawer | PR-Q45–48, 60, 76, 83 | BP-23, BP-08 |
| Audit log of sensitive actions | PR-Q24, 47, 82, 83 | BP-24 |
| Backup/restore: 23:00 + catch-up, verify, 30+12, USB, drill, safe restore | PR-Q53–57, 69, 84, 89 | BP-05, IMM-10 |
| Offline one-PC hosting: service, loopback, local config/keys/logs | PR-Q54, 64, 79 | BP-06, BP-09 |
| Images: JPG/PNG/WebP, 2 MB, 2000 px | PR-Q72–74 | IMM-10 |
| Bounded queries and paging | PR-Q58, 59, 63 (partial) | BP-11 |

## 2. Gaps and open items

| # | Gap | Why it is open | What is needed |
| --- | --- | --- | --- |
| G1 | **Receipt printer, scanner, drawer not tested on real devices** | PR-Q60/75/76: models not chosen yet | Owner buys/chooses devices; set `Printer:Mode`; run OAT-21/22. Code: ESC/POS over COM or Windows raw queue, drawer pulse, scanner as keyboard |
| G2 | **CQ-21 / CQ-22 rounding residual edge cases** | Owner has not answered | Interim behaviour in code: the residual (positive **or negative**) goes to the largest line/tender, first one when equal (`Money.Allocate`). Owner to confirm or choose another rule |
| G3 | **CQ-23 refund vs original cash rounding cap** | Owner has not answered | Interim (`RefundService`): the payout is first capped at money actually received for the sale (minus earlier payouts), then split in proportion to each tender, residual to Cash (else the first tender); a cash payout is rounded to whole taka and the difference saved as `PayoutRounding`. Owner to confirm |
| G4 | **PR-Q63 capacity** (sales/day, product count) | PENDING owner estimate | Paging and SQL aggregates are in place; measure with real volume (FUT-01) |
| G5 | Loyalty points | PR-Q32/70/71: "no points for now" | Nothing to build until a rule is given; customers are linked to sales |
| G6 | Password hashing strength check | PR-Q62: raise if login < 1 s on the shop PC | PBKDF2-SHA256 210,000 iterations now; time a login on the shop PC (INSTALLATION_CHECKLIST) |
| G7 | Shop-PC acceptance | Needs the installed PC | OFFLINE_TEST_REPORT rows marked Partly / Shop PC: offline cold boot, other-PC block, power cut, timed restore (RTO), SQL service permissions, replacement-Windows test |
| G8 | Images/settings archive timing | Design choice (documented) | Zip is taken right after the SQL backup, not under a pause (OAT-31). Acceptable for one PC; revisit only if needed |
| G9 | HTTPS on loopback | Optional | HTTP on 127.0.0.1 only; HTTPS supported via `Hosting:CertificatePath` if the owner wants it |
| G10 | Legacy pages kept as redirects | Compatibility | Old Reconciliation/Report/SalesAdmin URLs redirect to Shift/ReportAdmin/Refund; can be deleted in a later release |

## 3. Not in scope (owner decisions)
Cloud sync, multiple shops/PCs, online payments, e-commerce, SMS, VAT registration features beyond the Admin VAT/BIN settings,
loyalty redemption.
