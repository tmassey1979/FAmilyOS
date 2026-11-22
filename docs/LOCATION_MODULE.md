# FamilyOS location module

## Current milestone

The first location-sharing milestone now connects the MAUI app to authenticated FamilyOS APIs:

- Android device permission is while-in-use only and off by default.
- Household sharing is a separate server-side opt-in, also off by default.
- The user must explicitly request a location fix and separately tap to share it.
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

This is deliberately **not yet Life360-style continuous tracking**. There is no background service, location history, saved places, geofences, check-ins, or SOS delivery.

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
2. Android foreground location service with persistent notification, explicit start/stop, offline queue limits, battery controls, and a separate background permission flow.
3. Add map refresh automation only after explicit background-sharing consent, plus clear stale/offline states and member-level controls.
4. Saved places and deduplicated geofence transitions.
5. User-initiated check-ins and household SOS notifications with explicit delivery limitations.

## Security and release gates

- Never infer consent from household membership or a parent/guardian role.
- Stop accepting updates immediately after consent withdrawal or membership removal; member deactivation also deletes the stored record.
- Do not show coordinates to anyone outside the active household.
- Keep location history disabled until retention, deletion, and access auditing are designed.
- No default-on tracking, silent background collection, or emergency-dispatch claims.
- Physical Android tests must cover permission denial, approximate location, app process death, reboot, battery restrictions, offline delivery, membership removal, and consent revocation before background sharing is enabled in production.
