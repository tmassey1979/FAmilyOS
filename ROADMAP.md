# FamilyOS — Product delivery roadmap

**Production target:** Android release candidate and production readiness by **2027-01-16**.  
**Delivery window:** 58 days beginning 2026-11-19.  
**Canonical tracker:** [Issue #1 — FamilyOS Android production release](https://github.com/tmassey1979/FAmilyOS/issues/1).  
**Detailed plan:** [docs/RELEASE_PLAN_2027-01-16.md](docs/RELEASE_PLAN_2027-01-16.md).

This roadmap supersedes stale issue-number references in the previous roadmap. Delivery work is tracked in issues #2–#15 and #17–#19. The November 14 source audit is a starting baseline only; implementation status must be re-verified against current main and runtime evidence.

## Release strategy

- Android-first production release of the largest safe, complete portion of the FamilyOS core.
- Ship complete vertical slices: API + domain + persistence + authorization + mobile UI + tests.
- Keep core household coordination useful without paid/optional modules.
- Reduce optional scope before weakening security, privacy, offline correctness or release acceptance.
- Treat source presence as **not** proof of production readiness.

## P0 — Release gates (must pass)

- [ ] [#2 Authentication, onboarding and member access acceptance](https://github.com/tmassey1979/FamilyOS/issues/2)
- [ ] [#3 Authorization matrix, tenant isolation and privacy enforcement](https://github.com/tmassey1979/FamilyOS/issues/3)
- [ ] [#10 Offline-first synchronization and conflict recovery](https://github.com/tmassey1979/FamilyOS/issues/10)
- [ ] [#12 API integration, domain coverage and CI release gates](https://github.com/tmassey1979/FamilyOS/issues/12)
- [ ] [#13 Production security, privacy, retention, backups and observability](https://github.com/tmassey1979/FamilyOS/issues/13)

**Exit:** real Android auth/onboarding passes; cross-household authorization is tested; critical offline replay is safe; CI enforces quality gates; production security, restore and rollback are rehearsed.

## P1 — Core product journeys

- [ ] [#4 Household profiles, relationships, invitations and lifecycle](https://github.com/tmassey1979/FamilyOS/issues/4)
- [ ] [#5 Tasks, recurrence, routines and time tracking](https://github.com/tmassey1979/FamilyOS/issues/5)
- [ ] [#6 Generic requests, approvals and execution-plan commit](https://github.com/tmassey1979/FamilyOS/issues/6)
- [ ] [#7 Calendar editing, attendees, linked tasks/requests and sync](https://github.com/tmassey1979/FamilyOS/issues/7)
- [ ] [#8 Pulse, outcomes and activity timeline](https://github.com/tmassey1979/FamilyOS/issues/8)
- [ ] [#9 Notifications, push, deep links and realtime](https://github.com/tmassey1979/FamilyOS/issues/9)
- [ ] [#11 Shopping, procurement and meals end-to-end polish](https://github.com/tmassey1979/FamilyOS/issues/11)
- [ ] [#14 Android accessibility, settings and release UX](https://github.com/tmassey1979/FamilyOS/issues/14)

## P2 — Commercial and optional modules

- [ ] [#15 Feature flags, entitlements and optional module boundaries](https://github.com/tmassey1979/FamilyOS/issues/15)
- [ ] [#17 Google Play subscriptions, 30-day trial and commercial entitlement lifecycle](https://github.com/tmassey1979/FamilyOS/issues/17)
- [ ] [#18 Finance software: virtual envelopes, allowances and household reports](https://github.com/tmassey1979/FamilyOS/issues/18)
- [ ] [#19 Consent-based location sharing, places, check-ins and safety alerts](https://github.com/tmassey1979/FamilyOS/issues/19)

Finance issue #18 is virtual budgeting software only, not bank connectivity or stored-value accounts. Location issue #19 requires explicit revocable consent, strict retention and Android background-permission validation. SOS-style alerts are household notifications, not monitored emergency dispatch. These P2 modules must not delay core release gates and must remain disabled until their own acceptance criteria pass.

Smart home, health, school-provider integrations, advanced AI and other provider-dependent capabilities remain post-release candidates unless separately planned and validated.

## 58-day cadence

| Window | Focus | Exit evidence |
|---|---|---|
| Days 1–7 · Nov 19–25 | Auth/onboarding, security baseline, CI and audit reconciliation | Android auth proof, permission matrix, baseline CI |
| Days 8–21 · Nov 26–Dec 9 | Household, task planning, generic requests and execution plans | End-to-end family coordination journeys |
| Days 22–35 · Dec 10–23 | Calendar, Pulse, notifications, chat and offline | Linked workflows, deep links, replay tests |
| Days 36–45 · Dec 24–Jan 2 | Shopping, meals, settings and accessibility | Complete daily-operations workflows |
| Days 46–53 · Jan 3–10 | Security, integration, regression and device acceptance | Release-gate evidence and defect triage |
| Days 54–58 · Jan 11–15 | Release candidate, production rehearsal, rollback/restore | Signed release candidate and go/no-go checklist |
| **Release target · Jan 16** | **Production release decision** | **All mandatory gates green** |

## Definition of done

A story is complete only when its acceptance criteria pass in the running app/API, tests pass in CI, authorization and offline behavior are verified, and required Android/device acceptance is recorded. No story is closed solely because a screen, endpoint, domain model or test stub exists.

## Maintenance

Update [issue #1](https://github.com/tmassey1979/FAmilyOS/issues/1) and this roadmap when scope or dependencies change. Keep implementation evidence in the linked delivery issues.