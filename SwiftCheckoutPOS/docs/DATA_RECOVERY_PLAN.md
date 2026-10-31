# SwiftCheckoutPOS Data Recovery Plan

Status: canonical recovery design merged with approved owner objectives · 2026-10-31 (Asia/Dhaka). **Not executed; current backup code does not implement this plan.** No SQL, restore, migration, service or configuration action is authorized or performed by this documentation step.

## 1. Authority, scope and current limitations

This is the **single recovery document** for SwiftCheckoutPOS. [ARCHITECTURE.md](ARCHITECTURE.md) defines components; [PRODUCT_REQUIREMENTS.md](PRODUCT_REQUIREMENTS.md) records owner decisions; [OFFLINE_ACCEPTANCE_TESTS.md](OFFLINE_ACCEPTANCE_TESTS.md) records Step 5 cases. The historical audit is evidence, not an alternative recovery procedure.

One Windows PC, local SQL Server, local application/data/configuration/hardware. No cloud, network backup dependency, remote restore service or SQL Server Agent. The selected new-install baseline is SQL Server 2022 Express; a qualified licensed existing local instance can remain.

Current gaps: audit PB-11 raw SQL identifier/path construction; PB-12 download path containment; PB-13 unsafe/missing configuration defaults; PB-14 minute-only volatile scheduler; PB-15 no backup integrity verification or restore implementation; PB-21 installation-relative folder/ACL risk; PB-22 filename/FORMAT collision risk. Source evidence remains in [PROJECT_AUDIT.md](PROJECT_AUDIT.md), sections 6–7 and 13. These issues make the current presence of a .bak file insufficient evidence of recoverability.

No application or SQL connection was made in this task. **Actual SQL edition/version, recovery model, database size/identity, encryption, migration state, file locations, ACLs and backup validity are unknown.** All must be inventoried privately before operational use. A named development instance does not establish a production deployment.

## 2. Ownership and approved recovery objectives

| Responsibility | Required owner |
| --- | --- |
| Decide tolerable loss/time, retained generations and separate media | Shop owner, recorded against PR-Q53–57/PR-Q69 |
| Maintain service identities, SQL permissions, local certificates, media and guarded paths | Authorized local maintenance operator |
| Perform/approve destructive target replacement and confirm accepted recovery point | Authorized maintenance operator plus shop owner; never an ordinary cashier action |
| Inspect failures/overdue status and protect a verified removable copy | Assigned local operator; app must show actionable status |
| Qualify releases, restore drills and rollback compatibility | Maintainer using isolated synthetic test installation |

Approved owner objectives (31-10-2026): **RPO at most one business day of sales; RTO selling again within two hours**. A release requires measured evidence against these values, not a claim of execution. Source: OWNER_DECISIONS.md PR-Q54/69.

| Approved operating item | Requirement / owner source |
| --- | --- |
| Automatic full backup | Daily **23:00 Asia/Dhaka (UTC+6)**, one catch-up when PC/service was off; PR-Q53/89 |
| Upgrade protection | Manual verified pre-upgrade recovery set; PR-Q53 |
| Local generations | Last **30 daily +12 monthly** (first backup of each month); never delete newest verified; PR-Q55 |
| Separate copy | **Daily USB copy**, USB kept away from PC, copied hashes verified; PR-Q56 |
| Paired non-SQL files | Product images and settings files; virtual receipt text not required because saved-sale reprint must work; PR-Q57 |
| Restore drill | Latest set **monthly and before every upgrade**, isolated test database; PR-Q84 |
| Capacity | PR-Q63 PENDING; no invented volume or destructive purge |

Admin-authorized maintenance owns backup/download/restore/USB custody; Manager access is denied even if AllowManagerFinancialView is ON (PR-Q50/92).

**SIMPLE plus the approved daily verified full recovery sets is acceptable for the one-business-day RPO.** No point-in-time promise is made. Keep missed-run/USB-copy age visible so an overdue/unverified set is not falsely declared compliant. Existing recovery model/log chains must not be changed merely to simplify this app. FULL needs log backups; a periodic full backup is not a substitute for them. [Microsoft recovery model behavior](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/recovery-models-sql-server?view=sql-server-ver17).

## 3. Protected deployment inventory and recovery-set contents

Keep a private, ACL-protected installation inventory beside recovery metadata, with an offline protected copy. Public docs/logs contain **[REDACTED] and location only**, never real credentials, certificate keys or connection values.

Record these actual values privately before deployment/upgrade/restore:

- PC/Windows identity and supported OS, app release/runtime, configured local origin, SQL service/instance identity, actual edition/build/compatibility/collation and production licensing.
- Exact intended database identity/name, data/log file paths, SQL data size/headroom, recovery model/log-chain requirements, enabled encryption/features and latest reviewed migration ID/script hash.
- App/SQL/maintenance service identities, required SQL grants and file ACLs. Actual login/user SID mapping is part of a replacement-Windows recovery.
- Canonical install/data/config/media/log/backup paths, approved removable targets and independent verification of their resolved local paths; certificate reference/expiry and location of required encryption/recovery keys.
- Last successful backup/verification/copy/restore-drill times, approved RPO/RTO/retention, app/schema compatibility and pre-upgrade pinned recovery sets.

A **recovery set** includes:

| Component | Preservation requirement |
| --- | --- |
| Native SQL backup plus required log chain | All business tables, master references, local users/password hashes, settings, durable drafts/operation outcomes and business audit; complete files/LSNs/checksums recorded |
| Product/media files | Exact locally referenced media generation from the same maintenance boundary; manifest of relative IDs/hashes, not arbitrary source paths |
| Protected machine configuration and preferences inventory | Service/certificate/hardware/path policy, legacy backup_config.json preserved until reviewed migration is complete; no secrets in public metadata |
| App release and reviewed migration scripts | Offline package/native dependencies, release hash, from/to schema IDs and rollback compatibility |
| Key/certificate recovery material where actually needed | Required SQL encryption certificates/private keys if the installed database uses them; OS/disk recovery keys separately protected; machine-bound cookie keys are not assumed portable |
| Owner-classified business files | Product images and settings per PR-Q57 are required. Virtual receipt text files are not required; receipts must be reprintable from saved SQL snapshots (PR-Q48/83). Diagnostic logs have different retention and do not replace SQL audit |

A SQL .bak does **not** contain product images, machine.json, printer drivers, Windows accounts, browser cookies or arbitrary files. A copy on the same disk protects a narrower failure set than a verified separate removable copy. Keep the verified local set plus its daily USB copy away from the PC; no internet requirement. Include product images and settings, matching app/schema references and required recovery keys/metadata. Do not promise recoverable reprints until the saved-sale snapshot/reprint work is qualified.

Production backups contain personal data and password hashes. Restrict Windows ACLs and custody; use suitable OS/disk/removable-media encryption where available, with independently recoverable keys. Do not assume an Express edition provides every higher-edition database/backup encryption feature. Verify compatibility with any existing encrypted database before choosing the destination edition.

## 4. Paths, SQL construction and permissions

Target roots are outside replaceable binaries: default C:/ProgramData/SwiftCheckoutPOS/Backups, protected manifests below that root, and approved separate local/removable targets. SQL data/logs and media also remain outside the release folder. Never use the process working directory as a durable root.

Resolve and validate paths against configured approved roots. Reject UNC/remote locations, traversal, arbitrary absolute filenames and reparse-point escapes. User-facing selection/download uses opaque manifest IDs mapped server-side to verified files; a posted path or filename is never SQL authority.

Use parameterized SQL values/SQL variables for database/device values where the statement grammar permits. For syntax requiring an identifier, validate against the actual selected local database and use SQL Server QUOTENAME; a path is a value parameter, not an identifier. No concatenation of raw database names, filenames or settings. One reviewed native SQL implementation lives in DAL.

Distinguish permissions:

- App identity needs only necessary business DML/read and controlled backup permission, never routine sysadmin, database-owner schema deployment or destructive restore privilege.
- **SQL Server service identity** needs the required read/write access to the backup device and read access for verification/restore. App folder access does not establish SQL engine access.
- App identity needs manifest/config/status and approved copy/download rights only; ordinary Windows staff cannot modify backups/config/binaries/keys.
- Restore/schema provisioning uses a separately authorized maintenance SQL identity, outside normal app requests. Do not broaden the everyday app account to make recovery easy.

Check these rights using the actual installed identities in an isolated deployment test. BACKUP cannot run inside a user transaction; native backup/restore are separate maintenance operations, not the sales operation runner. A backup from a newer engine cannot be restored to an earlier one. [Microsoft backup restrictions and service-account permissions](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/create-a-full-database-backup-sql-server?view=sql-server-ver17).

## 5. Manual and automatic backup procedure

This procedure is the future common implementation used by manual backup and AutoBackupWorker.

1. Validate actual target/identity, schema compatibility, approved paths, free space, configuration and permissions. Missing settings or permissions produce a visible failed/overdue state, never an apparently successful silent disable.
2. Acquire the one-host backup gate. Request a **bounded maintenance pause** of new business/media/settings mutations, drain in-flight database units and preserve durable drafts. Do not abort a committed operation or force cancel customer work. If safely draining cannot complete, defer with a visible reason/overdue status. The approved due time is 23:00 Asia/Dhaka daily, with one missed-run catch-up; use the bounded pause without silently moving or skipping the schedule.
3. Allocate an attempt ID and new UTC timestamp-plus-random filename; record Started in an atomic protected manifest outside SQL. Never overwrite or FORMAT an existing backup. Serialize manual/automatic attempts.
4. Native SQL BACKUP uses CHECKSUM and failure-on-checksum-error behavior, outside any user transaction. Where FULL/log recovery applies, preserve the approved chain and metadata. Extra ad hoc full copies use a reviewed COPY_ONLY policy when needed to avoid disturbing another qualified differential strategy.
5. While mutations remain paused, capture required non-SQL media/configuration/release references and hashes. SQL backup plus those files describe the same stable boundary. Use staged temporary files and atomic publication; a partial copy is not a complete recovery set.
6. Read backup headers/file list, verify expected database/engine/schema/backup identity and run RESTORE VERIFYONLY with checksum checking. Compute hashes for completed files; record timestamps, LSNs where applicable, sizes and expected references. A failure keeps the attempt Failed/Unknown; it does not replace last-good metadata.
7. Publish the verified local set/manifest, release the mutation pause and record the exact recovery point. Never hold the POS paused while waiting indefinitely for removable media. Copy the immutable completed set to approved separate media when available; verify copied hashes and record copy status separately.
8. Show last local verification, separate-copy status, next due/overdue time and last error. If command acknowledgement is lost, inspect the same attempt/file and verify; **do not blindly replay with overwrite/new success claims**. Retain Unknown until resolved.
9. Qualify recoverability through section 7 restore drills; a SQL command succeeding or a file downloading is not a drill.

Native SQL backup itself can operate online; the short maintenance pause is a deliberate **single-PC consistency choice for paired SQL/media/configuration**, avoiding an elaborate snapshot subsystem. Measure pause duration with realistic retained data and ensure it fits the owner-approved operating window. No tax/stock policy is changed.

RESTORE VERIFYONLY checks backup readability/completeness and available checksums; it does not fully validate restored database structure or prove that the app can use the data. [Microsoft verification limits](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-verifyonly-transact-sql?view=sql-server-ver17).

## 6. Scheduling, restart, retention and status

Keep AutoBackupWorker in the Windows Service; Express has no SQL Server Agent. The singleton creates a scoped service for each attempt. It uses the same backup gate/procedure as manual backup, bounded cancellation/timeouts and visible local diagnostics.

Store authorized schedule preferences in SQL SystemSettings. Store attempts, next-due/last-verified checkpoint, manifests and copy/drill status in protected atomic files **outside the database being restored**; restoring SQL must not erase the only evidence of newer recovery sets. Validate/reconcile manifests on startup. Never trust file extension alone.

Calculate daily 23:00 instants in Asia/Dhaka independently of the Windows time zone. PR-Q53/89 require automatic backup; disabled state is a visible maintenance exception, not the normal shop default. After restart or an overdue period, run **one catch-up attempt** when readiness and maintenance conditions permit; do not require the app to be alive during one exact clock minute or generate a burst for every missed interval. Do not mark a failed/deferred attempt successful or advance last-verified time. Expose retry delay/overdue state; avoid busy loops and overlapping attempts. Disabled is explicit and visible, not the fallback for malformed/missing configuration.

If the service cannot run because SQL is unavailable, recovery status must remain visible through maintenance logs/status; backups cannot be promised while the PC is off. RPO/copy-age checks account for downtime, permission failures and absent removable media.

Retention keeps **30 latest daily sets plus 12 monthly sets**, selecting the first backup of each month (PR-Q55), with verified daily USB copy (PR-Q56). A monthly set that also occupies a daily slot is retained once and tagged for both requirements; do not fabricate missing months or count partial/failed attempts as a monthly recovery set. Always preserve the last verified usable set, pinned pre-upgrade/pre-restore sets and all files needed by retained log/differential chains. Do not delete unknown/partial files as a substitute for investigation. Never use the diagnostic log's 30-file rotation as backup or business-record retention.

After a restore, reconcile external manifests with the restored schema/data lineage and take a fresh verified baseline before considering normal backup coverage re-established. Do not silently treat a newer unrelated set or rolled-back SQL checkpoint as the current database's recovery point.

## 7. Verification and isolated restore drills

Use these distinct states: Started, Failed, OutcomeUnknown, BackupVerified (headers/checksums/VERIFYONLY/manifests), SeparateCopyVerified, and RestoreDrilled (actual restore plus integrity/business checks, with date/release). The UI must not collapse all of them into “Backup successful.”

Before production, **monthly and before every upgrade**, test-restore the latest recovery set and record results (PR-Q84), also after material engine/recovery changes. Test fixtures remain synthetic/isolated; the owner’s requirement to use the latest business backup for a private maintenance drill does not authorize a production-data clone in automated tests. A latest-business-set drill requires separately controlled authorized private handling and must never overwrite the business database. Record any synthetic-only limitation explicitly rather than claiming the real latest set was drilled. Step 5 uses **SwiftCheckoutPOS_Test** and fixture-owned roots per [ARCHITECTURE.md](ARCHITECTURE.md), never the business database or copied personal data.

Drill evidence includes accepted target guard, selected file hashes/header/chain, restored schema, SQL DBCC CHECKDB result, sale/item/tender/stock/ledger/refund/operation referential and approved arithmetic checks, media references, active/held recovery, account access and measured RTO. Capture record counts/known fixture IDs and expected totals before/after. CHECKDB does not prove business formulas.

No automated fixture reset/restore may use UI DbConn, developer secrets, real backup files, shop keys or a catalog merely ending in “Test.” The monthly latest-business-set drill is a separately authorized private maintenance activity on an isolated, explicitly guarded SwiftCheckoutPOS_Test target, not an automated synthetic fixture or the business database. Keep that private drill separate from test resets/fixtures and record protected handling; no such restore is performed here.

## 8. Restart, crash and power-loss recovery

For an ordinary crash or Windows restart:

1. Let SQL Server perform normal recovery; start the qualified SQL and app services. App startup performs read-only target/schema readiness checks, not migrations/recreation/seeding.
2. If SQL is not online/compatible or storage is unavailable, keep financial writes unavailable and show local maintenance status. Do not auto-detach, replace files, shrink logs, repair with data loss or restore a stale backup.
3. Reopen durable active/held work. Session loss may require local login/cart reconstruction; it must not remove SQL business records. Do not automatically complete/cancel/reassign drafts.
4. Resolve any outstanding OperationId against durable outcome. Return the committed sale/refund/stock result when found. If outcome is still unknown, keep Pending and investigate; a browser error/paper receipt is not proof of rollback/commit.
5. Show device delivery separately. A missing/ambiguous receipt can be reprinted under owner-approved copy rules; it cannot generate a new sale/refund or automatically repeat a drawer pulse.
6. Confirm backup age/status after recovery and catch up when safe; inspect local failure records.

SQL transaction durability does not prevent loss from failed disks, controllers ignoring flushes or damaged hardware. Use sound local storage, adequate disk headroom and qualified power protection; separate recovery media remains necessary. No automatic destructive “repair” is part of app startup.

## 9. Authorized restore procedure

Restore is maintenance while the app is stopped/unavailable, **not a regular cashier HTTP action**. The production app identity must not have blanket restore rights. This is a specification for a guarded future maintenance tool/operator procedure, not SQL to execute now.

1. **Confirm authorization and target.** Privately verify exact machine/instance/database identity, owner-approved recovery point/data-loss exposure, maintenance identity and compatible app/schema/SQL version. Record approval and incident/recovery ID without secrets.
2. **Preserve the starting state.** Stop new writes/backup worker; drain operations and stop the app service. Preserve manifests/media/config/release and the existing files/recovery set. Where safely possible, take a pre-restore copy/backup; for FULL-mode incidents have a qualified operator assess tail-log preservation before overwriting. Do not overwrite the only surviving evidence.
3. **Validate the selected set.** Verify custody/hashes/headers/checksums/file list and required log chain, correct source database, target engine/edition feature compatibility, schema/release pairing and sufficient space. Confirm target data/log paths cannot overwrite another database. An encrypted database needs its original required encryption material. Do not assume compatibility level enables engine downgrade. [Microsoft restore compatibility/encryption prerequisites](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/restore-a-database-backup-using-ssms?view=sql-server-ver17).
4. **Obtain exclusive target access.** Use a maintenance connection to master, not the database being replaced. Dispose/clear app connections/pools; prevent service/other tools reconnecting. Use controlled SINGLE_USER/exclusive access as required and reviewed force-disconnect only after drained/authorized work. Track the original access mode.
5. **Restore the reviewed chain.** Use the validated manifest/device and guarded database/file mapping; no raw UI path/name concatenation. Restore the full and applicable log chain to the agreed point, ending recovery only at the intended boundary. Native RESTORE is outside a user transaction. No automatic destructive replay after timeout/lost acknowledgement; inspect actual database state first.
6. **Validate before reopening.** Require ONLINE/expected schema, integrity checks, record/reference/approved-money checks, restored non-SQL files from the same set, matching configuration/release and correct local SQL login/user grants. Recheck engine/edition/capacity and local-only hosting. Do not insert fake migration-history or cost/actor values to make checks pass.
7. **Reopen deliberately.** Restore MULTI_USER only when the database is verified usable, readiness checks pass and maintenance operator approves. Reconcile external recovery manifests, obtain a new verified baseline, restart app/worker, and have the owner/operator check representative retained records and any accepted data gap. Record recovery point/RTO and exceptions. Resume selling only after explicit maintenance completion.

A web request cannot safely replace the database while its own scoped context/connections remain active. Keep future restore orchestration outside the running POS process; use a small local maintenance mode/tool and the same DAL native-SQL validation logic rather than granting the entire app sysadmin.

## 10. Failed restore and replacement-Windows recovery

If RESTORE fails or outcome is unknown, **keep the app/worker and business writes stopped**. Capture sanitized SQL error code/state and inspect ONLINE/RESTORING/RECOVERY_PENDING/SUSPECT status privately. Preserve original/current files and selected media. Never unconditionally set MULTI_USER in a finally block or start selling against an unvalidated target.

An authorized qualified operator chooses continuation of the reviewed restore chain, another independently verified compatible set, or a coordinated return to the preserved prior compatible state. That prior state may itself be damaged; do not promise rollback success. Escalate locally if integrity cannot be established. No app-driven REPAIR_ALLOW_DATA_LOSS, DROP/recreate, attach-guessing or forced migration-history editing. Record accepted loss before an irreversible replacement.

On a rebuilt Windows installation:

- Reprovision supported SQL/service identities, target paths/ACLs and local certificates from protected inventory/offline packages.
- Restore the compatible SQL/media/configuration set and map SQL users to the recreated Windows identity; same account spelling does not guarantee the same SID.
- Recover required SQL encryption keys/certificates and OS/media recovery keys independently. If they are unavailable, stop and report the actual recovery limitation.
- Machine-DPAPI cookie keys may be unusable on a fresh OS; provision a new key ring/certificate where necessary and require login. Losing old login tickets is distinct from losing SQL sales/users/password hashes.
- Apply the same verification/reopen gates. Do not treat a fresh OS as permission to create a blank shop database.

## 11. New-PC initialization and upgrades

For a genuinely new empty installation, the maintainer verifies an explicitly approved empty catalog, uses reviewed baseline SQL, provisions least-privilege identities/paths and controlled first Admin, then establishes the first verified recovery set and offline acceptance evidence. If any prior business database/files exist, use the preservation/upgrade path instead. No startup EnsureCreated/Migrate or public default Admin credential.

For every existing-data upgrade:

1. Inventory exact app/schema/SQL state and preserved data roots. Resolve audit migration drift against actual metadata; record differences privately.
2. Review explicit from/to generated EF migration SQL for destructive actions, FK/delete behavior, scale conversion, historical data and required backfills. Test the exact upgrade on a synthetic prior-version fixture and test the rollback plan.
3. Enter maintenance, drain writes and create/verify/pin the full recovery set **before schema changes**. Preserve the previous compatible app package/configuration and required recovery chain.
4. Apply only the reviewed script to the independently verified target using the separate maintenance identity. Ordinary startup never automatically applies it. Preserve IDs, business history, media and mappings; do not invent missing historical cost, actor or timestamps.
5. Verify schema/data/integrity/readiness with the matching new package; qualify print/device behavior and take a fresh baseline. Release maintenance only when checks pass.
6. If unsuccessful, binary rollback is allowed only when the prior binary is compatible with the resulting schema. Otherwise use the coordinated verified restore plan; restoring an earlier recovery point can discard later work and requires explicit accepted loss. Do not rely on a blind Down migration or overwrite only binaries and hope.

Preserve pinned pre-upgrade sets until the owner-approved retention/rollback window and a qualified newer recovery set permit retirement. A restore to a newer SQL engine may prevent returning the same database backup to the older engine; engine upgrades need their own reviewed rollback qualification.

## 12. Release evidence and remaining decisions

Required evidence is produced later, not claimed here: dedicated-test isolation, quoted-path/identity safety, permission failure behavior, completed paired set/copy hashes, VERIFYONLY plus actual restore, failed-restore containment, restart/catch-up, retained-record preservation and measured recovery time. Map Step 5 results to OAT cases and roadmap tasks.

PR-Q53/54/55/56/57/69/84/89 now supply frequency/time, RTO, retention, daily USB, files, RPO and drill cadence. These are approved **requirements**, not measured success. PR-Q63 capacity and actual SQL/OS/ACL/encryption/deployment inventory remain unverified. Demonstrate latest-set recovery plus paired images/settings/reprint, RPO ≤one business day and RTO ≤two hours before production; do not run a business restore/test in this documentation task.
