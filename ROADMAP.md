# FamilyOS — Product delivery roadmap

**Production target:** Android release candidate and production readiness by **2027-01-16**.  
**Delivery window:** 58 days beginning 2026-11-19.  
**Canonical tracker:** [Issue #1 — FamilyOS Android production release](https://github.com/tmassey1979/FAmilyOS/issues/1).  
**Detailed plan:** [docs/RELEASE_PLAN_2027-01-16.md](docs/RELEASE_PLAN_2027-01-16.md).

This roadmap covers core and add-on delivery stories #2–#15 and #17–#27. The November 14 source audit is a starting baseline only; implementation status must be re-verified against current main and runtime evidence.

## Release strategy

- Android-first production release of the largest safe, coherent FamilyOS platform.
- Ship complete vertical slices: API + domain + persistence + authorization + mobile UI + tests.
- Bring practical add-ons into the first release where a safe, bounded MVP fits: pet care, school coordination, caregiver access, home maintenance, multi-household foundations and universal search.
- Include safe foundations/prototypes for AI, smart home and wearables; enable external providers only after security, privacy and provider validation.
- Reduce optional scope before weakening security, privacy, offline correctness or release acceptance.

## P0 — Release gates (must pass)

- [ ] [#2 Authentication, onboarding and member access](https://github.com/tmassey1979/FAmilyOS/issues/2)
- [ ] [#3 Authorization, tenant isolation and privacy](https://github.com/tmassey1979/FAmilyOS/issues/3)
- [ ] [#10 Offline synchronization and conflict recovery](https://github.com/tmassey1979/FAmilyOS/issues/10)
- [ ] [#12 API integration, domain coverage and CI](https://github.com/tmassey1979/FAmilyOS/issues/12)
- [ ] [#13 Production security, retention, backups and observability](https://github.com/tmassey1979/FAmilyOS/issues/13)

## P1 — Core journeys and first-release add-on MVPs

- [ ] [#4 Household profiles and membership](https://github.com/tmassey1979/FAmilyOS/issues/4)
- [ ] [#5 Tasks, recurrence and routines](https://github.com/tmassey1979/FAmilyOS/issues/5)
- [ ] [#6 Requests, approvals and execution plans](https://github.com/tmassey1979/FAmilyOS/issues/6)
- [ ] [#7 Calendar and linked workflows](https://github.com/tmassey1979/FAmilyOS/issues/7)
- [ ] [#8 Pulse and household activity](https://github.com/tmassey1979/FAmilyOS/issues/8)
- [ ] [#9 Notifications, push and realtime](https://github.com/tmassey1979/FAmilyOS/issues/9)
- [ ] [#11 Shopping and meals](https://github.com/tmassey1979/FAmilyOS/issues/11)
- [ ] [#14 Accessibility, settings and Android UX](https://github.com/tmassey1979/FAmilyOS/issues/14)
- [ ] [#20 Pet profiles, care routines and reminders](https://github.com/tmassey1979/FAmilyOS/issues/20)
- [ ] [#22 School schedules, assignments and guardian workflows](https://github.com/tmassey1979/FAmilyOS/issues/22)
- [ ] [#23 Scoped caregiver access and care handoffs](https://github.com/tmassey1979/FAmilyOS/issues/23)
- [ ] [#26 Home assets and maintenance schedules](https://github.com/tmassey1979/FAmilyOS/issues/26)
- [ ] [#27 Multi-household context and permission-filtered universal search](https://github.com/tmassey1979/FAmilyOS/issues/27)

## P2 — Paid modules, integrations and advanced add-ons

- [ ] [#15 Feature flags, entitlements and module boundaries](https://github.com/tmassey1979/FAmilyOS/issues/15)
- [ ] [#17 Google Play subscriptions and trial](https://github.com/tmassey1979/FAmilyOS/issues/17)
- [ ] [#18 Virtual-envelope finance and allowances](https://github.com/tmassey1979/FAmilyOS/issues/18)
- [ ] [#19 Consent-based location, geofences and household safety alerts](https://github.com/tmassey1979/FAmilyOS/issues/19)
- [ ] [#21 Smart-home provider adapters and low-risk device integration](https://github.com/tmassey1979/FAmilyOS/issues/21)
- [ ] [#24 Permission-aware AI summaries and draft suggestions](https://github.com/tmassey1979/FAmilyOS/issues/24)
- [ ] [#25 Opt-in wearables and device signals](https://github.com/tmassey1979/FAmilyOS/issues/25)

P2 modules must remain disabled until their own consent, security, entitlement and runtime acceptance criteria pass. Finance issue #18 is virtual budgeting software, not bank connectivity or stored-value accounts. Location issue #19 requires revocable consent and retention controls; SOS is a household notification, not emergency dispatch. Smart-home control must fail safely and high-risk device actions remain excluded. AI may draft suggestions but cannot silently commit consequential actions. Wearable data is opt-in and minimized.

## 58-day delivery cadence

| Window | Focus | Exit evidence |
|---|---|---|
| Days 1–7 · Nov 19–25 | Auth, authorization, CI, audit reconciliation, multi-household data boundaries | Auth proof, role matrix, tenant isolation tests |
| Days 8–21 · Nov 26–Dec 9 | Household, tasks, requests, pet-care MVP and school task/calendar slice | Complete family workflows and role-safe add-on MVPs |
| Days 22–35 · Dec 10–23 | Calendar, Pulse, notifications, chat, offline, caregiver MVP | Handoff flow, deep links and offline replay proof |
| Days 36–45 · Dec 24–Jan 2 | Shopping, meals, home-maintenance MVP, universal search and settings | Linked cross-module workflows and search authorization tests |
| Days 46–53 · Jan 3–10 | Location/finance and AI/smart-home/wearable gates where feasible; security and regression | Independent privacy/security evidence; otherwise disabled |
| Days 54–58 · Jan 11–15 | Release candidate, production rehearsal, rollback/restore and store readiness | Signed RC and go/no-go evidence |
| **Jan 16** | **Production release decision** | **All P0 gates green; shipped modules independently accepted** |

## Definition of done

A story is complete only when acceptance criteria pass in the running app/API, tests pass in CI, authorization and offline behavior are verified, and required Android/device acceptance is recorded. Provider-dependent features may be represented by disabled adapters or documented follow-up work, but must not be advertised as active functionality.

## Maintenance

Update [issue #1](https://github.com/tmassey1979/FAmilyOS/issues/1) when scope changes. Keep implementation evidence in linked issues.