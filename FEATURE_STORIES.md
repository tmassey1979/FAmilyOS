# FamilyOS — Feature → Release Story Index

**Release target:** 2027-01-16  
**Canonical epic:** [#1 FamilyOS Android production release](https://github.com/tmassey1979/FAmilyOS/issues/1)

| Product area | Release story |
|---|---|
| Production authentication, PKCE and onboarding | [#2](https://github.com/tmassey1979/FAmilyOS/issues/2) |
| Authorization, privacy and tenant isolation | [#3](https://github.com/tmassey1979/FAmilyOS/issues/3) |
| Household profiles, relationships and member lifecycle | [#4](https://github.com/tmassey1979/FAmilyOS/issues/4) |
| Tasks, recurrence, routines and time tracking | [#5](https://github.com/tmassey1979/FAmilyOS/issues/5) |
| Dynamic requests, approvals and execution plans | [#6](https://github.com/tmassey1979/FAmilyOS/issues/6) |
| Shared calendar and linked workflows | [#7](https://github.com/tmassey1979/FAmilyOS/issues/7) |
| Pulse, blockers, outcomes and activity | [#8](https://github.com/tmassey1979/FAmilyOS/issues/8) |
| Notifications, push, quiet hours and realtime | [#9](https://github.com/tmassey1979/FAmilyOS/issues/9) |
| Offline cache, mutation queue and conflict recovery | [#10](https://github.com/tmassey1979/FAmilyOS/issues/10) |
| Shopping, procurement, meals and recipes | [#11](https://github.com/tmassey1979/FAmilyOS/issues/11) |
| API integration tests and CI release gates | [#12](https://github.com/tmassey1979/FAmilyOS/issues/12) |
| Production security, data lifecycle and operations | [#13](https://github.com/tmassey1979/FAmilyOS/issues/13) |
| Settings, accessibility and Android UX | [#14](https://github.com/tmassey1979/FAmilyOS/issues/14) |
| Feature flags, entitlements and module boundaries | [#15](https://github.com/tmassey1979/FAmilyOS/issues/15) |
| Google Play subscriptions and trial | [#17](https://github.com/tmassey1979/FAmilyOS/issues/17) |
| Virtual finance, envelopes and allowances | [#18](https://github.com/tmassey1979/FAmilyOS/issues/18) |
| Consent-based location, geofences and household alerts | [#19](https://github.com/tmassey1979/FAmilyOS/issues/19) |
| Pet profiles, care routines and reminders | [#20](https://github.com/tmassey1979/FAmilyOS/issues/20) |
| Smart-home adapter architecture and safe device integration | [#21](https://github.com/tmassey1979/FAmilyOS/issues/21) |
| School schedules, assignments and guardian coordination | [#22](https://github.com/tmassey1979/FAmilyOS/issues/22) |
| Scoped caregiver access, care plans and handoffs | [#23](https://github.com/tmassey1979/FAmilyOS/issues/23) |
| Permission-aware AI summaries and draft actions | [#24](https://github.com/tmassey1979/FAmilyOS/issues/24) |
| Opt-in wearable/device signals | [#25](https://github.com/tmassey1979/FAmilyOS/issues/25) |
| Home assets, maintenance and service requests | [#26](https://github.com/tmassey1979/FAmilyOS/issues/26) |
| Multi-household context, universal search and cross-module relationships | [#27](https://github.com/tmassey1979/FAmilyOS/issues/27) |

## First-release add-on targets

The January release should aim to include usable MVPs for pet care (#20), school coordination (#22), scoped caregiver access (#23), home maintenance (#26), and multi-household/search foundations (#27), provided the P0 release gates remain green.

Location (#19) and virtual finance (#18) are planned as optional paid modules with separate acceptance gates. Smart home (#21), AI (#24) and wearables (#25) should have safe architecture and a narrowly scoped integration/suggestion MVP only if provider, consent and security reviews pass. Otherwise, ship the module boundary disabled and continue post-release.

## Product acceptance journeys

1. A new family registers, creates a household and invites members.
2. Roles and household boundaries prevent unauthorized reads and writes.
3. Members assign and complete recurring tasks.
4. Members submit and approve schema-driven requests and explicitly commit reviewed execution plans.
5. Calendar events link to tasks, school activities, pet care and home maintenance.
6. Pulse shows accurate next actions, blockers and upcoming commitments.
7. Notifications and chat remain permission-safe and reliable through reconnect/offline conditions.
8. Shopping and meals connect recipes, requests, carts and purchase history.
9. Caregivers receive only time-bounded, explicitly scoped access.
10. Multi-household switching, offline caches and universal search never leak data.
11. Optional modules remain disabled without consent/entitlement and preserve data when disabled.
12. Production operations support secure deployment, monitoring, backups and rollback.

A story is complete only with passing acceptance criteria, CI evidence and runtime/device verification.