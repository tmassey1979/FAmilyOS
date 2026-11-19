# FamilyOS — Product delivery roadmap

Canonical tracking hub: [Issue #123 — FamilyOS roadmap and feature coverage](https://github.com/tmassey1979/FamilyOS/issues/123)

This roadmap is ordered by dependencies and release risk. Detailed requirements and acceptance criteria belong in linked issues. A feature is complete only after acceptance criteria, tests, CI and required Android/device validation pass.

## 60-day delivery target — January 14, 2027
- Target a usable, release-ready Android core by January 14, 2027; this is a delivery goal, not permission to bypass quality or safety gates.
- Minimum release candidate: Phase 0 exit gates plus the Phase 1 household coordination journeys, with the critical Phase 2 daily-operations paths selected and verified end to end.
- Treat Phases 3–4 as independently optional modules; defer integrations or specialist modules that cannot meet privacy, provider, testing and device-validation requirements by the target date.
- A release claim requires passing CI, tested authorization/offline behavior, accessibility checks, and physical Android validation of install/upgrade, authentication, permissions and critical workflows.
- Reassess scope daily from actual issue/PR/CI evidence. If schedule pressure rises, reduce optional scope rather than weaken release gates.

## Principles
- Android-first; core household coordination remains useful without optional modules.
- Major modules are independently enableable; add-on status does not imply paid packaging.
- Entitlements, authorization and explicit consent are separate controls. Feature flags never grant access.
- Server-side authorization is authoritative; offline replay must reconcile against current policy.
- Sensitive capabilities require granular access, consent, retention and audit rules.
- Prefer tested vertical slices and shared domain concepts.

## Phase 0 — Architecture and release gates (P0)
- [ ] [#111](https://github.com/tmassey1979/FamilyOS/issues/111) Feature flags, household entitlements and privacy controls.
- [ ] [#112](https://github.com/tmassey1979/FamilyOS/issues/112) Base vs optional module matrix.
- [ ] [#74](https://github.com/tmassey1979/FamilyOS/issues/74) Android auth/onboarding physical-device acceptance.
- [ ] [#78](https://github.com/tmassey1979/FamilyOS/issues/78) Offline replay and API integration tests.
- [ ] [#81](https://github.com/tmassey1979/FamilyOS/issues/81) Durable domain events.
- [ ] [#14](https://github.com/tmassey1979/FamilyOS/issues/14) Domain coverage target and enforcement.
- [ ] [#82](https://github.com/tmassey1979/FamilyOS/issues/82) Feature audit/backlog reconciliation.
- [ ] [#121](https://github.com/tmassey1979/FamilyOS/issues/121) Data export, deletion and retention.
**Exit:** catalog, authorization, consent, offline and CI gates are documented and tested.

## Phase 1 — Household foundation and core coordination (P1)
- [ ] [#119](https://github.com/tmassey1979/FamilyOS/issues/119) Member profiles and household directory.
- [ ] [#1](https://github.com/tmassey1979/FamilyOS/issues/1), [#17](https://github.com/tmassey1979/FamilyOS/issues/17) Authentication, household context, onboarding/invites.
- [ ] [#2](https://github.com/tmassey1979/FamilyOS/issues/2), [#13](https://github.com/tmassey1979/FamilyOS/issues/13), [#77](https://github.com/tmassey1979/FamilyOS/issues/77) Tasks, assignments, recurrence and planning.
- [ ] [#75](https://github.com/tmassey1979/FamilyOS/issues/75), [#76](https://github.com/tmassey1979/FamilyOS/issues/76) Schema-driven requests and execution-plan review/commit.
- [ ] [#79](https://github.com/tmassey1979/FamilyOS/issues/79) Calendar editing, attendees and task-linked events.
- [ ] [#120](https://github.com/tmassey1979/FamilyOS/issues/120) Universal search and cross-module linking.
**Exit:** members can onboard, coordinate tasks/requests/events and navigate linked records safely.

## Phase 2 — Daily operations and communication (P1/P2)
- [ ] [#98](https://github.com/tmassey1979/FamilyOS/issues/98) Daily briefing.
- [ ] [#99](https://github.com/tmassey1979/FamilyOS/issues/99) Activity timeline.
- [ ] [#80](https://github.com/tmassey1979/FamilyOS/issues/80), [#9](https://github.com/tmassey1979/FamilyOS/issues/9), [#27](https://github.com/tmassey1979/FamilyOS/issues/27), [#46](https://github.com/tmassey1979/FamilyOS/issues/46) Pulse, notifications, preferences and quiet hours.
- [ ] [#117](https://github.com/tmassey1979/FamilyOS/issues/117) Messaging, announcements and shared notes.
- [ ] [#103](https://github.com/tmassey1979/FamilyOS/issues/103) Chore rotations and routines.
- [ ] [#116](https://github.com/tmassey1979/FamilyOS/issues/116) Goals and nonfinancial rewards.
- [ ] [#122](https://github.com/tmassey1979/FamilyOS/issues/122) Accessibility and localization.
**Exit:** members can understand priorities, communicate and coordinate recurring work accessibly.

## Phase 3 — Practical household modules (P2)
- Shopping/procurement: #5, #10, #23, #24, #34, #44, #64.
- Meals/recipes: #12, #58 (base/optional decision in #112).
- Transportation: [#104](https://github.com/tmassey1979/FamilyOS/issues/104).
- Home/property: [#113](https://github.com/tmassey1979/FamilyOS/issues/113).
- Pets: [#114](https://github.com/tmassey1979/FamilyOS/issues/114).
- Travel/outings: [#118](https://github.com/tmassey1979/FamilyOS/issues/118).
**Exit:** optional modules integrate with tasks, calendar, notifications and documents without bypassing permissions.

## Phase 4 — Sensitive and external integrations (opt-in; P2/P3)
- GPS/maps/geofences: #96, #100, #101.
- Check-ins/SOS: [#102](https://github.com/tmassey1979/FamilyOS/issues/102).
- Documents: [#108](https://github.com/tmassey1979/FamilyOS/issues/108).
- Finance: [#109](https://github.com/tmassey1979/FamilyOS/issues/109).
- Health: [#115](https://github.com/tmassey1979/FamilyOS/issues/115).
- Smart home/automation: [#106](https://github.com/tmassey1979/FamilyOS/issues/106).
- School/activity integrations: [#107](https://github.com/tmassey1979/FamilyOS/issues/107).
- AI assistant: [#105](https://github.com/tmassey1979/FamilyOS/issues/105).
- Memories/milestones: [#110](https://github.com/tmassey1979/FamilyOS/issues/110).
**Exit:** explicit consent, scoped credentials, disconnect/revocation, provider review and privacy tests; sensitive modules are never on by default.

## Phase 5 — Release readiness (continuous)
- Keep #82 as verified feature inventory.
- Every feature issue documents behavior, acceptance criteria, dependencies, roles/consent, offline behavior, export/retention and tests.
- CI runs Android build, unit/integration tests, static analysis and accessibility checks.
- Validate install/upgrade, auth, permissions, offline recovery and critical flows on physical Android devices.

## Coverage index
- Household identity/profiles: #1, #17, #119
- Tasks/chores/routines: #2, #13, #20, #43, #47, #52, #77, #103
- Calendar: #7, #49, #79
- Requests/approvals/execution: #3, #4, #18, #21, #22, #28, #36, #39, #40, #41, #42, #48, #62, #75, #76
- Dashboard/activity: #6, #29, #30, #35, #80, #98, #99
- Notifications/realtime: #9, #26, #27, #32, #46, #50, #80, #81
- Shopping/meals/transport: #5, #10, #12, #23, #24, #34, #44, #58, #64, #104
- Location/safety/maps: #96, #100, #101, #102
- AI/automation: #105, #106
- Documents/privacy: #108, #121
- Finance: #109; school: #107; memories: #110
- Home: #113; pets: #114; health: #115; goals: #116; messaging: #117; travel: #118
- Search: #120; accessibility: #122
- Quality/offline/release: #14, #38, #61, #66, #74, #78, #82

## Maintenance
Update this file and [issue #123](https://github.com/tmassey1979/FamilyOS/issues/123) when priorities/dependencies change. Implementation status belongs in individual issues, not this document.
