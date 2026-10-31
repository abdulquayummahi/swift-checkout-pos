# SwiftCheckoutPOS Offline Acceptance Tests

**Cases only. All cases: NOT EXECUTED — Step 5.** No pass/fail result or production readiness is asserted.

## Common preconditions for every case

- Dedicated synthetic **SwiftCheckoutPOS_Test** on an explicitly allowlisted local SQL test instance; fixture marker/file roots verified before any create/reset/restore. No real business database, real backup, real personal data or shop configuration/key ring.
- Isolated test host/environment/service, loopback endpoint, generated local test accounts/keys and fixture-owned media/backup/log paths. Test settings cannot fall back to UI DbConn/User Secrets/production environment. Reset follows [ARCHITECTURE.md](C:/source/repos/SwiftCheckoutPOS/docs/ARCHITECTURE.md), section 12.
- Financial expected examples are owner-approved under [PRODUCT_REQUIREMENTS.md](C:/source/repos/SwiftCheckoutPOS/docs/PRODUCT_REQUIREMENTS.md) question IDs. A case needing an unanswered rule is **Blocked by that question**, not passed by copying existing behavior.
- Recovery actions follow only [DATA_RECOVERY_PLAN.md](C:/source/repos/SwiftCheckoutPOS/docs/DATA_RECOVERY_PLAN.md). Failure/restart/power-loss simulation uses the isolated test installation; never interrupt live shop work. Physical-device cases require an explicitly assigned test device/operator and no real payment or cash movement.
- Step 5 evidence records app/schema/SQL/OS/device versions, case/fixture IDs, actions, sanitized actual results, test time, record IDs/counts and outcome. No secret values, customer data, raw credentials or cookies in results.

## OAT-01 — Test-target guard and reset safety

**Trace:** NFR-10; BP-10.

**Preconditions:** Valid synthetic fixture plus invalid connection variants: absent, wrong catalog, remote server, unmarked existing catalog and unexpected data/log path.

**Actions:** Attempt test host setup/reset/backup/restore with each invalid variant; then create/reset the marked valid fixture twice.

**Expected:** Invalid variants are rejected before opening a business database or changing any SQL/files. Only the exact approved instance/catalog/marker/roots can be created/reset; unrelated catalogs/files remain identical. Unit tests never connect to SQL.

## OAT-02 — Clean-cache offline installation and cold boot

**Trace:** FR-15/16; NFR-01; BP-06/BP-09.

**Preconditions:** Isolated supported Windows installation; qualified offline package/SQL/driver/certificate prerequisites; external connectivity disabled and browser cache cleared.

**Actions:** Install the test deployment offline, reboot without opening a developer shell or manually starting services; log in locally and exercise catalog/cart/customer/report/backup screens.

**Expected:** SQL/app services become ready, local shortcut works, required assets load from local origin, no CDN/font/API/auth/license request blocks a core feature. No SDK, dotnet restore, internet or interactive service-account login is needed.

## OAT-03 — Machine configuration and SQL identity

**Trace:** FR-12/16; BP-06/BP-12.

**Preconditions:** Test machine file and SQL identity; variants missing/invalid DbConn, wrong schema, stopped SQL and unauthorized connection override.

**Actions:** Start each variant, including Production environment/CLI override attempts; restore the valid fixture configuration afterward.

**Expected:** Clear safe maintenance/unavailable status; no developer/test/shop fallback, silent blank database creation, migration or secret disclosure. Correct configured local identity has required business rights and no routine schema/restore/sysadmin power.

## OAT-04 — Loopback, HTTPS and certificate behavior

**Trace:** NFR-01/06; BP-06.

**Preconditions:** Isolated test certificate and endpoint; explicit wildcard/LAN configuration variants.

**Actions:** Inspect effective listeners; attempt non-loopback binding override, normal localhost browser access and access via a non-loopback interface. Test expired/untrusted certificate handling and controlled replacement offline.

**Expected:** Only loopback is bound; unsafe endpoint overrides prevent readiness. HTTPS is trusted locally with the provisioned certificate; private key ACLs are restricted. No public HTTP/LAN listener or dependence on a development certificate.

## OAT-05 — Keys, login and durable work across restart

**Trace:** FR-01/05/16; NFR-02; IMM-02/BP-03/BP-06.

**Preconditions:** Still-valid test auth ticket, durable active/held work and completed sale with recorded IDs.

**Actions:** Restart app/service and reboot isolated Windows; reopen browser/login as appropriate. Repeat after replacing compatible binaries while retaining ProgramData/SQL.

**Expected:** Still-valid authorized tickets remain decryptable with preserved key ring/app identity; expiry/revocation still applies. Durable records/work/outcomes survive; lost Session is reconstructed. No reseed or new sale is inferred. Fresh-OS keys may require login as documented, without losing SQL facts.

## OAT-06 — Anonymous, role and direct-URL enforcement

**Trace:** FR-01; PR-Q50; BP-07.

**Preconditions:** Active Cashier/Manager/Admin fixture accounts and owner-approved route-role matrix.

**Actions:** Request every protected action directly with each role and anonymously, including GET/POST variants, forged posted actor/role and hidden menu URLs.

**Expected:** Server enforces the approved matrix independently of UI; cashier cannot administer users/backup/finance through direct URLs. Actor comes from validated identity; Admin is not implicitly treated as Manager unless explicitly approved.

## OAT-07 — Revocation, hashing, throttle and bootstrap

**Trace:** FR-01; PR-Q51/52/62/64/68/78; IMM-09/BP-07/BP-13.

**Preconditions:** Synthetic user store, versioned password records and controlled empty-catalog bootstrap fixture.

**Actions:** Disable/demote/reset a logged-in account from another session; attempt concurrent removal of last active Admin; test password verify/rehash/invalid formats, configured failed-login delays, inactivity/expiry and controlled first-Admin setup.

**Expected:** Stale privileges are revoked, last Admin remains recoverable under approved policy, malformed hashes fail safely, no default password or public repeatable bootstrap. Throttle/expiry follow approved limits and do not leak account/secrets. Recovery stays locally authorized.

## OAT-08 — Antiforgery and mutation methods

**Trace:** NFR-05; IMM-02/BP-07.

**Preconditions:** Logged-in test users, all state-changing routes inventoried.

**Actions:** Submit missing/invalid/valid tokens for cart, loyalty, quick-add, print/drawer, inventory, finance, refunds, settings and user actions. Request former loyalty mutation URL with GET.

**Expected:** Missing/invalid tokens cause no mutation/hardware action. Valid requests work within role gates; GET never changes cart/records. Local validation assets load in correct order without internet.

## OAT-09 — Monetary precision and approved examples

**Trace:** FR-02/04/08/09; PR-Q01–13/67; IMM-05/BP-01.

**Preconditions:** Approved cost/retail/tax scales, rounding stages/midpoints, tax and discount examples including half-cent/mixed-rate values.

**Actions:** Create/edit/restock products with fractions, perform an unrelated edit, calculate/complete a sale and compare DTO/SQL/report/receipt values; submit excessive scale and invalid numeric strings.

**Expected:** No unintended whole-number round-trip loss or display mismatch; authoritative decimal calculations match approved examples. Invalid values are rejected before persistence; formatting never substitutes for a calculation rule.

## OAT-10 — Sale atomicity and retry boundary

**Trace:** FR-04; NFR-03; IMM-04/BP-02.

**Preconditions:** Completed checkout implementation, fixture with required sale/items/tender/stock/ledger/customer/audit effects; controlled faults before/after each persistence stage.

**Actions:** Finalize once normally; inject validation/nontransient failures and retryable SQL faults at read/save/commit stages.

**Expected:** Complete unit commits or none does; no partial stock/ledger/customer change. User transaction runs inside CreateExecutionStrategy with fresh per-attempt state; EnableRetryOnFailure remains. Print/files are outside retry delegate.

## OAT-11 — Double submit, two tabs and unknown commit

**Trace:** NFR-04; IMM-04/BP-02.

**Preconditions:** Same durable draft/OperationId, second stale tab, unique receipt index and a lost-commit-acknowledgement simulation.

**Actions:** Double-click, refresh/replay, concurrently submit same request, reuse ID with different payload and replay after uncertain commit; query outcome after reconnect.

**Expected:** Exactly one sale and one set of effects, stable receipt/outcome; conflicting payload fails without mutation. Unknown stays Pending until reconciled, never recommends new operation identity. Concurrent completion of the same draft with different IDs is also prevented.

## OAT-12 — Stock/product validation and stale forms

**Trace:** FR-02/04; PR-Q14/22/23/24/77/82; IMM-06/BP-02.

**Preconditions:** Approved negative-stock/inactive-product/adjustment rules; stale original RowVersion and two tabs.

**Actions:** Sell beyond availability, change/deactivate/remove referenced product after cart load, concurrently restock/sell/edit the same product, submit a stale metadata form and forged stock/actor fields.

**Expected:** Approved commit-time policy is enforced against fresh rows, structural missing-product failures cannot partially commit, stale edits require explicit review. Metadata edits never overwrite newly committed stock; adjustments are authorized and attributable.

## OAT-13 — Hold/resume/cancel/expiry and crash

**Trace:** FR-05; PR-Q15–18/49; BP-03.

**Preconditions:** Approved reservation/ownership/cancel/expiry rules and legacy held-order migration fixture.

**Actions:** Park/resume twice from two tabs; crash between SQL change and response/session reconstruction; restart/transfer/cancel/expire according to approved policy.

**Expected:** Original durable work and links survive; no delete-then-session loss, duplicate reservation/restoration or silent expiry loss. Legacy IDs/receipt references are preserved through reviewed mapping. No workflow is passed before policy is answered.

## OAT-14 — Restock/vendor return and ledger consistency

**Trace:** FR-02/08; PR-Q16/19–25; IMM-04/IMM-08.

**Preconditions:** Approved valuation/acquisition/zero-cost/vendor-credit examples; held stock and stock movements present.

**Actions:** Restock and return with valid/invalid/boundary inputs, retry/replay and inject ledger/stock save failures; test held quantities in valuation.

**Expected:** Approved stock/cost/ledger/audit effects commit together exactly once. Malformed decimal never becomes accepted zero; invalid type/category/retail is rejected. No financial treatment is inferred for no-cash returns.

## OAT-15 — Refund state, quantity and repeat protection

**Trace:** FR-07; PR-Q26–31/65/66/71; IMM-04/BP-04.

**Preconditions:** Approved refund eligibility/tender/tax/cost/customer/stock-disposition policy and immutable source sale.

**Actions:** Refund completed, held, missing and already-refunded receipts; race/replay two refund requests and inject failures. If partial refunds are approved, test cumulative quantities against sold quantities.

**Expected:** Only approved transitions/effects occur, atomic once, with occurrence date/actor/source linkage. Historical sale is preserved, no repeated stock restoration or excessive cumulative refund. Unsupported partial refund is clearly unavailable, not assumed required.

## OAT-16 — Ledger and manual input validation

**Trace:** FR-08; PR-Q33/34; IMM-08.

**Preconditions:** Approved category/direction combinations and synthetic malformed requests.

**Actions:** Submit whitespace/case variants of reserved categories, unsupported type, negative/zero/excess-scale amount and bypassed client validation; record permitted transactions.

**Expected:** Normalize before allowlist checks; unknown direction never becomes Outflow. ModelState and BLL agree; required business audit exists. Indicators use only approved semantics.

## OAT-17 — COGS, profit, filters and dates

**Trace:** FR-09/10; PR-Q31/35–40/58/59/79–81; IMM-07/BP-04/BP-11.

**Preconditions:** Approved indicator equations/date zone/boundaries and known fixture costs/taxes/refunds/expenses across date boundaries.

**Actions:** Run period/lifetime/search reports; alter current product cost after a sale; test exact end boundary, month/year transitions and later refund occurrence.

**Expected:** Approved metrics match independent expected values; historical completion-cost measures do not change with present cost. Missing historical facts are explicitly unknown rather than fabricated. Date/search scope matches approved definitions, with no boundary loss/double inclusion.

## OAT-18 — Till reconciliation and real actor

**Trace:** FR-11; PR-Q41–44; IMM-09/BP-04.

**Preconditions:** Approved float/shift/cash/refund/blind-count definitions and multiple actors/shifts.

**Actions:** Submit a nonzero counted amount, malformed input, duplicate close, stale/concurrent close and actor spoof; compare both legacy and current workflows.

**Expected:** Exact validated count and authenticated actor persist once; approved cash equation/boundary applies consistently. No hardcoded user, fabricated eight-hour shift or silently accepted zero-binding failure.

## OAT-19 — Customers and loyalty

**Trace:** FR-06; PR-Q32/70/71; BP-02/BP-04.

**Preconditions:** Approved earning/redemption/refund rules and synthetic customers.

**Actions:** Associate/change customer using POST, complete/replay sale and refund/replay; restart during active customer-linked work.

**Expected:** Local-only profiles/history, authorized access and durable association; approved points changes are atomic with sale/refund and applied once. No guessed points policy is accepted as expected behavior.

## OAT-20 — Media/path/XSS and safe errors

**Trace:** FR-02; NFR-06; PR-Q61/72–74; IMM-10/BP-07.

**Preconditions:** Approved barcode/image rules and fixture media tree with sentinel files.

**Actions:** Submit wildcard/traversal/quote barcode, invalid image bytes/oversize/dimensions, validation failure after file staging, script-like text and forced exception; attempt backup download traversal.

**Expected:** Safe generated media IDs and bounded validated content; no arbitrary deletion/escape/remote image fetch. Failed edits preserve old media. Razor/JS safely encode input; UI/logs contain safe correlation IDs, no exception detail/secrets.

## OAT-21 — Saved-sale printing, failure and reprint

**Trace:** FR-14; PR-Q45/48/60/83; BP-08.

**Preconditions:** Synthetic committed sale, fake/assigned physical printer, explicit virtual mode and approved receipt/copy examples.

**Actions:** Complete sale with device absent/disconnected/busy/timeout; delay/fail printing before/after response, replay completion and reprint saved receipt after restart.

**Expected:** Sale remains committed once with same IDs/values; device outcome separately Printed/Failed/Unknown. No precommit print or automatic replay on ambiguous delivery. Reprint uses historical saved facts and approved copy labeling; virtual mode is explicitly shown.

## OAT-22 — Barcode scanner and drawer

**Trace:** FR-04/14; PR-Q47/75/76; BP-08.

**Preconditions:** Assigned test hardware or fake; approved tender/drawer policy; empty test drawer/no money.

**Actions:** Use keyboard/manual/wedge input with focus changes; test serial reader only if required; disconnect/reconnect. Trigger/replay print/drawer requests and fail device during a saved sale.

**Expected:** No device attached still permits authorized manual sale entry. Bounded local reconnect/error behavior, correct configured protocol, one intended drawer action without blind retry. No hardware failure rolls back/repeats money/stock.

## OAT-23 — Local logs and diagnostic failure

**Trace:** NFR-06/07; BP-06.

**Preconditions:** Fixture log directory/ACLs and enough synthetic events to roll configured files.

**Actions:** Cross date/size thresholds, deny log write/fill fixture storage, inject sensitive-shaped values, complete a sale and restart the isolated host.

**Expected:** Daily/size rolling with 30-file limit as configured, bounded growth/event handling, local fallback. No sensitive values/raw requests/SQL/receipt contents. Diagnostic failure never changes established business outcome; durable SQL audit remains.

## OAT-24 — Manual backup correctness and permission failures

**Trace:** FR-13; IMM-10/BP-05.

**Preconditions:** Synthetic SQL/media/configuration set and quoted approved fixture path; separate app/SQL identities.

**Actions:** Follow recovery-plan backup procedure; remove SQL write/app manifest access separately, simulate full disk, invalid path/database name and simultaneous manual/auto request.

**Expected:** Safe parameter/identifier handling, no overwrite/traversal, distinct permission diagnosis, serialized bounded maintenance pause. Partial failure is not BackupVerified; last good set is retained and stock/sales stay consistent.

## OAT-25 — Scheduler restart, catch-up and missing configuration

**Trace:** FR-13/16; PR-Q53/58/69/79; BP-05/BP-12.

**Preconditions:** Approved enabled schedule, controllable test clock, durable external checkpoint; disabled/malformed variants.

**Actions:** Restart before/after due time, miss schedules, fail/defer one attempt, alter local clock/time-zone boundary and restore an earlier test DB.

**Expected:** One overdue catch-up when safe, no exact-minute dependence/overlap/burst or false success. Explicit disabled status differs from invalid configuration. Checkpoints survive DB rollback and are reconciled with restored lineage.

## OAT-26 — Verification, separate copy and retention

**Trace:** FR-13; PR-Q55–57/84; BP-05.

**Preconditions:** Complete/partial/tampered/pinned fixture sets, approved retention/media rules and last verified set.

**Actions:** Verify headers/checksums/hashes, copy to fixture removable target, disconnect it, exercise retention and a restore drill via canonical recovery plan.

**Expected:** BackupVerified/SeparateCopyVerified/RestoreDrilled remain distinct. Tampering/missing files fail; absent media is visible. No deletion of last good/pinned/chain-required set; actual restore/integrity/reference evidence is required for RestoreDrilled.

## OAT-27 — Restore and failed-restore containment

**Trace:** FR-13; PR-Q54/69; BP-05.

**Preconditions:** Exact guarded synthetic test target with recorded recovery point, compatible recovery set and corrupt/incompatible/partial variants.

**Actions:** Follow canonical restore procedure with open test connections, wrong target/path/engine and failures at restore/recovery/validation stages; then a valid isolated restore.

**Expected:** Invalid targets rejected before replacement; app stays stopped through failure/unknown state. No unconditional MULTI_USER, destructive auto repair, blind replay or false readiness. Valid restore includes SQL/media/configuration compatibility and checks, recorded RTO and explicit reopen.

## OAT-28 — Crash/power-loss and storage recovery

**Trace:** FR-16; NFR-02/03/04; IMM-04/BP-06.

**Preconditions:** Isolated installation/fault simulation and known fixture IDs/outstanding operations; never live hardware power interruption.

**Actions:** Interrupt process/isolated power simulation before save, during transaction and after commit acknowledgement loss; restart with SQL delayed or fixture storage unavailable.

**Expected:** SQL recovery plus operation lookup establishes no partial/duplicate business unit. Durable active/held work remains available after readiness. No blank DB/reseed, stale auto-restore or new-operation retry. Device delivery remains separate.

## OAT-29 — Reviewed migration and compatible upgrade

**Trace:** NFR-02/08; IMM-03/BP-06.

**Preconditions:** Synthetic prior-version fixture with historical records, reviewed SQL and matching offline old/new packages.

**Actions:** Follow canonical upgrade procedure, attempt startup with schema mismatch, simulate failed migration/deployment and exercise documented compatible rollback on test target.

**Expected:** Verified pinned pre-change recovery set precedes modification; no startup migration. IDs/amounts/actors/media are preserved, unknown historical values not fabricated. Binary rollback requires compatible schema; coordinated restore handles incompatible state with recorded recovery point.

## OAT-30 — Bounded queries, indexes and capacity

**Trace:** FR-10; NFR-09; PR-Q63; BP-11.

**Preconditions:** Owner-agreed representative synthetic history/volume and selected SQL edition.

**Actions:** Capture query plans/rows/durations for barcode search, product lists, period/lifetime reports; approach fixture disk/capacity thresholds and repeat core operations.

**Expected:** Bounded/paged/aggregated SQL, useful modeled indexes applied through reviewed migrations, no N+1/full-history tracking graph for ordinary cards. Visible capacity failure without partial sales or deleting history; measured targets documented, not invented.

## OAT-31 — Backup boundary under active work

**Trace:** FR-04/05/12/13; BP-03/BP-05.

**Preconditions:** Durable draft and concurrent fixture media/settings/product changes.

**Actions:** Request backup while a write is in flight; exceed the permitted drain window; later allow a full paired set and restore it using the recovery plan.

**Expected:** Drain waits for established transactions, deferred backup is visibly overdue, no forced customer cancellation. Restored SQL references match captured media/settings; no half-published set and no indefinite checkout pause.

## OAT-32 — Offline tender labels and local-only traffic

**Trace:** FR-04/15; PR-Q46; BP-01/BP-02/BP-09.

**Preconditions:** Owner-approved offline Cash/Card/Mobile Wallet confirmation behavior and network capture in isolated installation.

**Actions:** Complete synthetic transactions for each supported label offline, inspect browser/process outbound requests, deny external DNS/traffic and reload cleared-cache screens including stored images.

**Expected:** No mandatory external payment/API/telemetry/license/config dependency. UI truthfully records approved operator confirmation rather than pretending to authorize a payment provider. Stored media remains local and no remote URL is fetched.

## OAT-33 — Replacement-Windows recovery and least privilege

**Trace:** FR-01/13/16; NFR-06; BP-05/BP-06/BP-13.

**Preconditions:** Isolated replacement-Windows test installation, synthetic recovery set and separate recovery material inventory.

**Actions:** Follow replacement-Windows section of canonical recovery plan with recreated service identity; test missing key/certificate and incorrect SID/ACL variants.

**Expected:** Correctly restored SQL business records/users survive; new cookie keys can require login. Required encryption keys cannot be bypassed; missing material yields explicit stopped recovery. Local-only host and least-privilege grants/ACLs are verified before reopen.
