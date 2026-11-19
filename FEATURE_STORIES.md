# FamilyOS — Feature → Release Story Index

**Release target:** 2027-01-16  
**Canonical epic:** [#1 FamilyOS Android production release](https://github.com/tmassey1979/FamilyOS/issues/1)

This index replaces the previous story-number mapping, which referenced issue IDs that are not present in the current repository issue tracker. The new issues are production-oriented vertical slices; each includes acceptance criteria and dependencies.

| Product area | Release story |
|---|---|
| Production authentication, PKCE, household onboarding, invitations and member access | [#2](https://github.com/tmassey1979/FamilyOS/issues/2) |
| Roles, household isolation, privacy and server-side authorization | [#3](https://github.com/tmassey1979/FamilyOS/issues/3) |
| Household profiles, relationships, member lifecycle and ownership | [#4](https://github.com/tmassey1979/FamilyOS/issues/4) |
| Tasks, recurrence, routines, assignment/acceptance and time tracking | [#5](https://github.com/tmassey1979/FamilyOS/issues/5) |
| Dynamic requests, approvals, conditions, decision history and execution plans | [#6](https://github.com/tmassey1979/FamilyOS/issues/6) |
| Shared calendar, editing, attendees, linked tasks/requests and Google sync | [#7](https://github.com/tmassey1979/FamilyOS/issues/7) |
| Pulse, blockers, outcomes and household activity | [#8](https://github.com/tmassey1979/FamilyOS/issues/8) |
| In-app/push notifications, quiet hours, deep links and realtime | [#9](https://github.com/tmassey1979/FamilyOS/issues/9) |
| Offline cache, mutation queue, replay, idempotency and conflicts | [#10](https://github.com/tmassey1979/FamilyOS/issues/10) |
| Shopping queue, carts, purchases, meal planning and recipes | [#11](https://github.com/tmassey1979/FamilyOS/issues/11) |
| API integration tests, domain coverage, mobile checks and CI gates | [#12](https://github.com/tmassey1979/FamilyOS/issues/12) |
| Production security, data lifecycle, backups, observability and recovery | [#13](https://github.com/tmassey1979/FamilyOS/issues/13) |
| Settings, onboarding guidance, accessibility and Android release UX | [#14](https://github.com/tmassey1979/FamilyOS/issues/14) |
| Module flags, entitlements, consent and optional-module boundaries | [#15](https://github.com/tmassey1979/FamilyOS/issues/15) |
| Google Play subscriptions, Core trial, add-on packaging and billing lifecycle | [#17](https://github.com/tmassey1979/FamilyOS/issues/17) |
| Finance software: virtual envelopes, allowances and reports (no bank accounts) | [#18](https://github.com/tmassey1979/FamilyOS/issues/18) |
| Consent-based location sharing, geofences, check-ins and household SOS alerts | [#19](https://github.com/tmassey1979/FamilyOS/issues/19) |

## Product scenarios / release acceptance

1. A new family can install, register, create a household, invite members and sign in again.
2. Each role can perform permitted actions and cannot access another household's records.
3. Members can assign, accept, decline, complete, defer and schedule recurring responsibilities.
4. Members can create schema-driven requests; approvers can decide, set conditions and commit reviewed execution plans.
5. Members can create/edit/cancel shared events and navigate linked tasks/requests.
6. Pulse surfaces accurate next actions, blockers and upcoming commitments for the signed-in role.
7. Members can communicate and receive permission-safe notifications with working deep links.
8. Shopping and meal workflows connect recipes, requests, carts and purchase history.
9. Core workflows recover safely after offline use, app restart, token expiry and connectivity restoration.
10. Production operations support secure deployment, observability, backup/restore and rollback.

## Completion rule

A release story is not complete because code exists. It must satisfy its issue acceptance criteria in the current branch, pass CI, and have runtime/device evidence where specified. Optional modules must remain independently disabled unless their own safety and release gates pass.

See [ROADMAP.md](ROADMAP.md) and [docs/RELEASE_PLAN_2027-01-16.md](docs/RELEASE_PLAN_2027-01-16.md).