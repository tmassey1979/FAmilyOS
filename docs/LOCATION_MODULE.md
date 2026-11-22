# FamilyOS location module

## Current milestone

The first location-sharing milestone now connects the MAUI app to authenticated FamilyOS APIs:

- Android device permission is while-in-use only and off by default.
- Household sharing is a separate server-side opt-in, also off by default.
- The user must explicitly request a location fix and separately tap to share it.
- The server stores only the latest shared fix, not a location history.
- Active members of the same household can see the latest fix from members who opted in.
- Stopping sharing clears the stored coordinates immediately.
- Disabling device location also attempts to revoke household sharing.
- The API rejects invalid coordinates, inaccurate values outside its accepted bounds, timestamps more than five minutes in the future, and fixes older than 24 hours.
- The API checks active household membership on consent, update, and read operations.

This is deliberately **not yet Life360-style continuous tracking**. There is no background service, live map, location history, saved places, geofences, check-ins, or SOS delivery.

## API

- `GET /api/location/consent` — current member's sharing state.
- `PUT /api/location/consent` — explicit opt-in/out. Opt-out deletes the saved fix.
- `POST /api/location/current` — submit one user-initiated location fix after consent.
- `GET /api/location/family` — latest fixes shared by active members of the caller's household, limited to the last 24 hours.

The initial sharing scope is all active members of the same household. Per-viewer grants and more granular sharing controls are not implemented yet.

## Required next milestones

1. Per-viewer sharing grants, member-level privacy controls, and guardian/child consent rules.
2. Android foreground location service with persistent notification, explicit start/stop, offline queue limits, battery controls, and a separate background permission flow.
3. A map UI with freshness/accuracy indicators and clear stale/offline states.
4. Saved places and deduplicated geofence transitions.
5. User-initiated check-ins and household SOS notifications with explicit delivery limitations.

## Security and release gates

- Never infer consent from household membership or a parent/guardian role.
- Stop accepting updates immediately after consent withdrawal or membership removal.
- Do not show coordinates to anyone outside the active household.
- Keep location history disabled until retention, deletion, and access auditing are designed.
- No default-on tracking, silent background collection, or emergency-dispatch claims.
- Physical Android tests must cover permission denial, approximate location, app process death, reboot, battery restrictions, offline delivery, membership removal, and consent revocation before background sharing is enabled in production.
