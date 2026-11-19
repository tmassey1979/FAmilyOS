# FamilyOS Android production release plan — 2027-01-16

**Planning baseline:** 2026-11-19  
**Target:** 2027-01-16 (58 days from baseline)  
**Master issue:** [#1](https://github.com/tmassey1979/FamilyOS/issues/1)

## Release objective

Ship the largest safe and coherent Android production release of FamilyOS by January 16, 2027. The core release includes household coordination, tasks, requests/approvals, calendar, Pulse, shopping, meals, messaging, notifications, settings and reliable offline behavior.

Location tracking, SOS, finance/banking, smart home, health, school-provider integrations and advanced provider-dependent AI are explicitly out of the critical path. They may ship only if their independent privacy, consent, security, provider and device validation is complete. Never enable them by default.

## Work packages

| Issue | Work package | Priority |
|---|---|---|
| [#2](https://github.com/tmassey1979/FamilyOS/issues/2) | Authentication, onboarding and member access acceptance | P0 |
| [#3](https://github.com/tmassey1979/FamilyOS/issues/3) | Authorization matrix, tenant isolation and privacy | P0 |
| [#4](https://github.com/tmassey1979/FamilyOS/issues/4) | Household profiles, relationships and lifecycle | P1 |
| [#5](https://github.com/tmassey1979/FamilyOS/issues/5) | Tasks, recurrence, routines and time tracking | P1 |
| [#6](https://github.com/tmassey1979/FamilyOS/issues/6) | Dynamic requests, approvals and execution-plan commit | P1 |
| [#7](https://github.com/tmassey1979/FamilyOS/issues/7) | Calendar editing, attendees and linked workflows | P1 |
| [#8](https://github.com/tmassey1979/FamilyOS/issues/8) | Pulse, outcomes and activity timeline | P1 |
| [#9](https://github.com/tmassey1979/FamilyOS/issues/9) | Notifications, push, deep links and realtime | P1 |
| [#10](https://github.com/tmassey1979/FamilyOS/issues/10) | Offline queue, replay and conflict recovery | P0 |
| [#11](https://github.com/tmassey1979/FamilyOS/issues/11) | Shopping, procurement and meals | P1 |
| [#12](https://github.com/tmassey1979/FamilyOS/issues/12) | API tests, coverage and CI release gates | P0 |
| [#13](https://github.com/tmassey1979/FamilyOS/issues/13) | Production security, privacy, backups and observability | P0 |
| [#14](https://github.com/tmassey1979/FamilyOS/issues/14) | Accessibility, settings and Android release UX | P1 |
| [#15](https://github.com/tmassey1979/FamilyOS/issues/15) | Feature flags, entitlements and optional module boundaries | P2 |
| [#17](https://github.com/tmassey1979/FamilyOS/issues/17) | Google Play subscriptions, trial and commercial entitlement lifecycle | P2 |

## Calendar

### Days 1–7 · November 19–25 — Prove the foundation
- Reconcile the November 14 audit against current main.
- Validate PKCE, token lifecycle, household creation/join and invite acceptance on physical Android.
- Establish role/permission matrix and cross-household negative tests.
- Measure current CI/test/coverage baseline and identify release-blocking defects.
- Verify production configuration excludes development auth bypass and demo credentials.

**Exit:** authentication/onboarding proof, authorization design, CI baseline and prioritized defect list.

### Days 8–21 · November 26–December 9 — Complete family coordination
- Finish household profile/member lifecycle gaps.
- Complete task recurrence, routines, planned/actual time and timezone handling.
- Implement generic request questionnaire rendering from server schemas.
- Complete execution-plan review and explicit commit on mobile.
- Add API integration tests for household, tasks and request workflows.

**Exit:** a family can onboard, assign responsibilities, submit/decide requests and commit resulting work.

### Days 22–35 · December 10–23 — Connect daily workflows
- Complete calendar editing, attendees and task/request links.
- Finish Pulse, outcomes and activity timeline.
- Complete notification delivery, quiet-hours enforcement and deep links.
- Verify chat reconnect/poll fallback and selected realtime updates.
- Prove offline replay ordering, idempotency, token refresh and conflict recovery.

**Exit:** members can coordinate events and communication; offline work synchronizes safely.

### Days 36–45 · December 24–January 2 — Finish practical modules and UX
- Verify shopping/cart/purchase and meal/recipe/ingredient workflows end to end.
- Complete settings, onboarding guidance and device preferences.
- Run accessibility and localization-readiness review.
- Resolve core-flow UI defects; avoid introducing new high-risk modules.
- Verify migration behavior on clean install and upgrade.

**Exit:** core daily operations are complete, understandable and stable.

### Days 46–53 · January 3–10 — Stabilization and security
- Run full API/mobile regression and authorization matrix.
- Run physical-device tests for install/upgrade, auth, offline, notifications and deep links.
- Validate production secrets, TLS, logging, alerts and data lifecycle.
- Rehearse encrypted backup restore, migration and rollback.
- Fix P0/P1 defects; freeze scope.

**Exit:** all mandatory release gates have evidence; no unresolved critical/high security issue.

### Days 54–58 · January 11–15 — Release candidate
- Build signed Android release candidate and verify package/version/signing.
- Run final smoke/regression suite against production-like deployment.
- Verify app update/install path and store metadata/assets.
- Confirm monitoring, support/incident response, backup and rollback readiness.
- Make explicit go/no-go decision; no scope expansion.
- Complete Google Play billing only if it does not displace P0/P1 release gates; otherwise defer paid activation.

**January 16:** production release decision and rollout only if all mandatory gates are green.

## Release gates

- Auth: real Keycloak OIDC/PKCE sign-in, refresh, logout and household onboarding on physical Android.
- Authorization: role matrix and cross-household isolation automated.
- Data: migrations tested on clean install and upgrade; export/deletion/retention behavior documented.
- Offline: durable queue, idempotency, ordering, retry, token expiry and conflict recovery verified.
- Quality: API integration tests, domain tests, mobile typecheck/build, coverage gate and green CI.
- Security: no production dev bypass/secrets; least privilege; sensitive modules opt-in and off by default.
- Operations: health/readiness, logs/metrics/alerts, encrypted backups, restore and rollback rehearsed.
- Android: install/upgrade, navigation, accessibility, notification permission, deep links and critical journeys tested on physical devices.
- Commercial: do not sell or advertise paid plans/trials until Google Play purchase verification, entitlement lifecycle, cancellation/refund behavior and store policy review are complete.

## Scope-control rules

1. P0 release gates cannot be waived.
2. If schedule slips, defer optional integrations and reduce polish before cutting tests or authorization.
3. Do not advertise vendor product catalogs, bank connectivity, SOS response or location safety guarantees unless implemented and operationally validated.
4. Keep virtual budgeting envelopes distinct from regulated financial accounts.
5. Do not store or share location without explicit, revocable consent.
6. Close issues only with acceptance evidence linked in the issue.

## Baseline caveat

The source audit in `docs/FEATURE_AUDIT_2026-11-14.md` is useful evidence of code presence and known gaps, but it predates this plan and is not a runtime acceptance report. Re-audit the current default branch before marking any story complete.