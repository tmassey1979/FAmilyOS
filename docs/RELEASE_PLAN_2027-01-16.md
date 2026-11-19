# FamilyOS Android production release plan — 2027-01-16

**Planning baseline:** 2026-11-19  
**Target:** 2027-01-16 (58 days from baseline)  
**Master issue:** [#1](https://github.com/tmassey1979/FAmilyOS/issues/1)

## Release objective

Ship a coherent Android FamilyOS release with the core household platform plus as many safe add-on MVPs as can be completed and verified. Core includes household coordination, tasks, requests, calendar, Pulse, shopping, meals, messaging, notifications, settings and offline reliability.

The first-release add-on targets are pet care, school coordination, scoped caregiver access, home maintenance, multi-household context and universal search. Location and virtual finance are separately gated optional modules. Smart home, AI and wearables get provider-neutral foundations and narrowly scoped MVPs only after independent security, consent and provider validation.

## Work packages

| Issue | Work package | Priority |
|---|---|---|
| [#2](https://github.com/tmassey1979/FAmilyOS/issues/2) | Authentication and onboarding | P0 |
| [#3](https://github.com/tmassey1979/FAmilyOS/issues/3) | Authorization, privacy and tenant isolation | P0 |
| [#4](https://github.com/tmassey1979/FAmilyOS/issues/4) | Household profiles and member lifecycle | P1 |
| [#5](https://github.com/tmassey1979/FAmilyOS/issues/5) | Tasks, recurrence and routines | P1 |
| [#6](https://github.com/tmassey1979/FAmilyOS/issues/6) | Dynamic requests and approvals | P1 |
| [#7](https://github.com/tmassey1979/FAmilyOS/issues/7) | Calendar and linked workflows | P1 |
| [#8](https://github.com/tmassey1979/FAmilyOS/issues/8) | Pulse and activity | P1 |
| [#9](https://github.com/tmassey1979/FAmilyOS/issues/9) | Notifications and realtime | P1 |
| [#10](https://github.com/tmassey1979/FAmilyOS/issues/10) | Offline replay and conflict recovery | P0 |
| [#11](https://github.com/tmassey1979/FAmilyOS/issues/11) | Shopping and meals | P1 |
| [#12](https://github.com/tmassey1979/FAmilyOS/issues/12) | API tests and CI quality gates | P0 |
| [#13](https://github.com/tmassey1979/FAmilyOS/issues/13) | Production security and operations | P0 |
| [#14](https://github.com/tmassey1979/FAmilyOS/issues/14) | Accessibility, settings and Android UX | P1 |
| [#15](https://github.com/tmassey1979/FAmilyOS/issues/15) | Feature flags and entitlement boundaries | P2 |
| [#17](https://github.com/tmassey1979/FAmilyOS/issues/17) | Google Play subscriptions and trial | P2 |
| [#18](https://github.com/tmassey1979/FAmilyOS/issues/18) | Virtual-envelope finance and allowances | P2 |
| [#19](https://github.com/tmassey1979/FAmilyOS/issues/19) | Consent-based location and safety alerts | P2 |
| [#20](https://github.com/tmassey1979/FAmilyOS/issues/20) | Pet profiles, care tasks and reminders | P1/P2 |
| [#21](https://github.com/tmassey1979/FAmilyOS/issues/21) | Smart-home adapters and safe device integration | P2 |
| [#22](https://github.com/tmassey1979/FAmilyOS/issues/22) | School schedules, assignments and guardian workflows | P1/P2 |
| [#23](https://github.com/tmassey1979/FAmilyOS/issues/23) | Caregiver access and handoff workflows | P1/P2 |
| [#24](https://github.com/tmassey1979/FAmilyOS/issues/24) | Permission-aware AI summaries and draft actions | P2 |
| [#25](https://github.com/tmassey1979/FAmilyOS/issues/25) | Wearable/device signals | P2 |
| [#26](https://github.com/tmassey1979/FAmilyOS/issues/26) | Home assets, maintenance and service requests | P1/P2 |
| [#27](https://github.com/tmassey1979/FAmilyOS/issues/27) | Multi-household, universal search and cross-module relationships | P1/P2 |

## 58-day execution schedule

### Days 1–7 · Nov 19–25 — Foundation and architecture
- Reconcile source audit with current main; identify existing implementations before duplicating work.
- Validate Keycloak PKCE, token lifecycle, household onboarding and invite acceptance on Android.
- Establish complete role/permission matrix and cross-household negative tests.
- Define module contracts, entitlements, consent and data-retention conventions.
- Design household-context isolation for API, local database, caches and offline queue.
- Baseline CI, migrations, test coverage and release-blocking defects.

**Exit:** authentication evidence, permission matrix, module contracts and CI baseline.

### Days 8–21 · Nov 26–Dec 9 — Core workflows plus pet/school MVPs
- Complete household/member lifecycle, tasks, recurrence, routines and request execution.
- Pet MVP: profile, feeding/walking/grooming tasks and vet appointment reminders.
- School MVP: student schedule, assignment tracking, due dates and calendar links.
- Add shared task/calendar links and API integration coverage.

**Exit:** usable family coordination plus pet and school records in the normal task/calendar workflows.

### Days 22–35 · Dec 10–23 — Connected workflows and caregiver MVP
- Complete calendar, Pulse, notifications, chat and offline conflict recovery.
- Caregiver MVP: invitation, time-bounded scoped access, selected tasks/events and handoff log.
- Add universal search across core records with strict per-record authorization.
- Implement household switcher foundations with isolated local cache and queued mutations.
- Test notification deep links across tasks, requests, school and pet-care records.

**Exit:** caregivers can coordinate safely; search and household context do not leak data.

### Days 36–45 · Dec 24–Jan 2 — Home operations and optional module foundations
- Home maintenance MVP: asset register, recurring maintenance, service request and calendar visit.
- Complete shopping, meals, settings and accessibility.
- Finance MVP only if billing/entitlement and data-security prerequisites are satisfied; otherwise retain virtual-envelope module behind disabled entitlement.
- Location MVP only if consent, background permission, retention and revocation tests pass.
- Establish smart-home adapter, AI assistant and wearable connector boundaries; only low-risk provider-backed slices may proceed.
- Cross-module links must preserve authorization when a module is disabled.

**Exit:** core daily operations and selected add-on MVPs are complete; optional sensitive modules have independent go/no-go decisions.

### Days 46–53 · Jan 3–10 — Integration and hardening
- Run full API/mobile regression and role/household authorization matrix.
- Validate location revocation, finance boundaries and any enabled provider integration.
- Test offline replay across multi-household context switches.
- Test search leakage, AI prompt injection and draft-only action behavior if AI MVP is enabled.
- Physical-device test install/upgrade, background permissions, notifications, deep links and battery behavior.
- Validate backups, restore, retention, deletion and operational alerts.

**Exit:** all enabled modules pass independent acceptance; no unresolved critical/high security issue.

### Days 54–58 · Jan 11–15 — Release candidate
- Build signed Android release candidate.
- Run production-like smoke/regression and verify store assets/metadata.
- Rehearse backup restore, rollback and incident response.
- Verify every advertised paid module has purchase verification, server-side entitlements, cancellation/refund lifecycle and clear terms.
- Disable any incomplete add-on rather than ship a broken or unsafe feature.
- Record final go/no-go and release evidence.

**January 16:** production rollout only when P0 gates and every enabled module's acceptance criteria are green.

## Release gates

- Auth and onboarding work with real OIDC/PKCE on physical Android.
- Role, caregiver, child and household permissions are enforced server-side and covered by negative tests.
- Household context partitions local caches, search, sync queues and all server queries.
- Offline operations are durable, ordered, idempotent and recover correctly after auth expiry/conflicts.
- Data export/deletion, retention, consent revocation and migrations are tested.
- No production development bypass, embedded secrets or overbroad provider tokens.
- Location, finance, AI, smart-home and wearable modules are disabled unless separately accepted.
- Smart-home high-risk controls are never unattended by default; AI suggestions never silently execute.
- Backup/restore, monitoring, alerting and rollback are rehearsed.
- Android install/upgrade and critical journeys pass on physical devices.

## Scope-control rules

1. P0 security/reliability gates cannot be waived.
2. Target the pet, school, caregiver, home-maintenance and search/multi-household MVPs as first-release add-ons; reduce their breadth rather than leave them as empty shells.
3. Optional sensitive modules can ship disabled with clear follow-up acceptance criteria.
4. Do not claim emergency dispatch, medical monitoring, financial accounts, provider integrations or guaranteed location accuracy unless actually delivered and operationally validated.
5. Close stories only with CI, runtime and device evidence.

## Baseline caveat

The November 14 feature audit is a source-presence baseline, not a runtime acceptance report. Re-audit current main before implementation and avoid duplicating completed code.