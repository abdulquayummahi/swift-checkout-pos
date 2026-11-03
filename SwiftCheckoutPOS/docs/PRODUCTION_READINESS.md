# Production Readiness (playbook Step 6)

Date: 2026-11-04. Decision: **ready for installation and acceptance testing on the shop PC; not yet ready for live sales**
until the "Before go-live" items below are ticked.

## Ready
- [x] All owner rules implemented except open edge questions (FEATURE_GAP_REPORT G2/G3) — interim behaviour documented
- [x] 39/39 unit, 146/146 integration tests pass, including the in-memory Production host test
- [x] All 37 DR differences fixed; roadmap IMM/BP tasks done or code-done
- [x] Database: one clean migration chain, model = migrations test, `--migrate` maintenance command, no automatic migration at start
- [x] Security: fallback sign-in policy, roles per PR-Q50, antiforgery on every POST, 12-h login + 15-min lock, throttle,
      revocation, no default password, safe error page, security headers, loopback-only listener, AllowedHosts
- [x] Privacy: Manager financial view OFF by default, Admin-only toggle with audit
- [x] Backup/restore: verified daily backup with catch-up, retention, USB copy, drill and safe restore commands
- [x] Deployment: self-contained package, Windows service, ProgramData data root with restricted ACLs, least-privilege SQL script,
      local logs, DPAPI key ring — [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md), [INSTALLATION_CHECKLIST.md](INSTALLATION_CHECKLIST.md), [UPGRADE_GUIDE.md](UPGRADE_GUIDE.md)
- [x] No secrets in the repository; connection string only in `appsettings.Local.json` on the PC or user secrets on the developer PC

## Before go-live (owner)
- [ ] Merge the feature branches into `main` in order (see README) and build the package with `deploy\build-package.ps1`
- [ ] Install on the shop PC and complete [INSTALLATION_CHECKLIST.md](INSTALLATION_CHECKLIST.md)
- [ ] Choose and test the receipt printer, scanner and drawer (G1)
- [ ] Answer CQ-21, CQ-22, CQ-23 or accept the interim behaviour (G2/G3)
- [ ] Give the PR-Q63 volume estimate (G4)
- [ ] Do the first restore drill and a timed restore; record the time against the 2-hour RTO
- [ ] Decide on the old development database (UPGRADE_GUIDE "From the development versions")

## Known limits
- One PC only, by design. The till is not reachable from other computers.
- Backups protect against PC failure only when the USB copy is made and kept away from the PC.
- Hardware output is untested until devices are chosen; the virtual printer writes text files in `DataRoot\VirtualReceipts`.
