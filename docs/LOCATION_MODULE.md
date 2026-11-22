# FamilyOS location module

## Current milestone

The first location-sharing milestone now connects the MAUI app to authenticated FamilyOS APIs:

- Android device permission is while-in-use only and off by default.
- An explicitly started Android location foreground service now attempts periodic updates (about once per minute), shows an ongoing notification with a Stop action, checks server sharing consent before each update, and does not request background location permission or restart itself after process death.
- Household sharing is a separate server-side opt-in, also off by default.
- The user must explicitly request a location fix and separately tap to share it. Future continuous sharing must be started from a visible screen and run as an Android location foreground service with a persistent notification; the app will not silently restart tracking after process death or reboot.
- The server stores only the latest shared fix, not a location history.
- A member's location is private by default; only individually granted active household members can see the latest fix.
- Each member manages their own viewer list; household membership or guardian role never grants location access automatically.
- Revoking one viewer removes their access immediately. Revoking sharing clears coordinates and all grants.
- Stopping sharing clears the stored coordinates immediately.
- Deactivating a household member deletes their stored location record.
- Disabling device location also attempts to revoke household sharing.
- The API rejects invalid coordinates, inaccurate values outside its accepted bounds, timestamps more than five minutes in the future, and fixes older than 24 hours.
- The API checks active household membership on consent, update, and read operations.

The MAUI app now includes a household map that plots the latest explicitly shared fixes, with last-shared time and reported accuracy. Map tiles are loaded from OpenStreetMap and include attribution. The map is refreshed on demand; it is not a live feed.

The Android foreground service is the first continuous-tracking implementation, not yet a production-validated Life360 equivalent. It must pass Android APK CI and physical-device testing for Android-version behavior, notification actions, permission denial, process death, battery restrictions, offline behavior, and immediate consent revocation. It currently has no bounded offline queue, adaptive movement/distance thresholds, or cross-device status heartbeat. Location history, saved places, geofences, check-ins, SOS delivery, and driving safety are still unimplemented.

## API

- `GET /api/location/consent` — current member's sharing state.
- `PUT /api/location/consent` — explicit opt-in/out. Opt-out deletes the saved fix.
- `POST /api/location/current` — submit one user-initiated location fix after consent.
- `GET /api/location/family` — latest fixes explicitly shared with the caller by active household members, limited to the last 24 hours.
- `GET /api/location/viewers` — active household members and whether the current member has granted them location visibility.
- `PUT /api/location/viewers/{viewerMemberId}` — explicitly grant or revoke one active household member's access.

Per-viewer grants are enforced by the API. A location owner can grant or revoke access to each active household member. Existing opt-in sharers from before this privacy change have no viewer grants by default and must explicitly re-allow each person. Grants are separate from the owner's location-sharing consent and do not submit or collect coordinates. New members receive no grants. Member deactivation deletes grants in either direction. Sharing revocation deletes all grants owned by that member and erases the saved fix. The location-grant table is provisioned by the existing idempotent location schema ensure step; it does not require an EF migration.

## Required next milestones

1. Guardian/child consent rules and independent, age-appropriate consent review.
2. Validate and harden the Android location foreground service: confirm visible-activity start restrictions, stop action behavior, bounded offline queue, battery-aware sampling, tracking heartbeat/status, and immediate consent revocation. Retain while-in-use location permission; do not request `ACCESS_BACKGROUND_LOCATION` unless a separately justified background-start use case is added.
3. Add authenticated location freshness/status metadata and map refresh automation while the viewer is active, with stale/offline states and member-level controls.
4. Saved places, configurable geofence radii, and deduplicated arrival/departure transitions with notification preferences.
5. Location history and trip summaries with explicit opt-in, retention limits, export, and deletion controls.
6. User-initiated check-ins, household SOS workflows, device push delivery, delivery receipts, and clear emergency-service limitations.
7. Driving trip detection and driving insights; crash detection only after sensor validation and false-positive handling.

## Security and release gates

- Never infer consent from household membership or a parent/guardian role.
- Stop accepting updates immediately after consent withdrawal or membership removal; member deactivation also deletes the stored record.
- Do not show coordinates to anyone outside the active household.
- Keep location history disabled until retention, deletion, and access auditing are designed.
- No default-on tracking, silent background collection, or emergency-dispatch claims.
- Physical Android tests must cover permission denial, approximate location, app process death, reboot, battery restrictions, offline delivery, membership removal, and consent revocation before background sharing is enabled in production.
