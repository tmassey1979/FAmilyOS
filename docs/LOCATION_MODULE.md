# FamilyOS location module

## Current MAUI milestone

The MAUI client currently provides a foreground-only, user-initiated location reading:

- Location access is off by default.
- The user must explicitly enable access on the device.
- Android requests only the while-in-use location permission.
- A location fix is requested only after the user taps the action.
- Coordinates are held in memory by the page and are not uploaded or persisted.
- The user can turn off the local consent setting at any time.

This is a device-location foundation, **not Life360-style household sharing**. There is no live family map, background tracking, location history, saved places, geofences, check-ins, or SOS delivery in this milestone.

## Required next milestones

1. Server-side member consent and sharing grants, including immediate revocation and recipient scoping.
2. Authenticated location ingestion and household map query APIs with freshness and accuracy metadata.
3. Android foreground location service with a persistent notification, explicit start/stop, offline queue limits, and battery controls. Background permission must be requested separately with an explanation and only after the user enables a feature that needs it.
4. Saved places and deduplicated geofence transitions.
5. User-initiated check-ins and household SOS notifications with explicit delivery limitations.

## Security and release gates

- The server must verify module entitlement, active household membership, member consent, and the specific viewer grant on every read/write.
- Never infer consent from household membership or a parent/guardian role.
- Stop accepting updates immediately after consent withdrawal or membership removal.
- Minimize retention, provide deletion, and audit access.
- No default-on tracking, silent background collection, or emergency-dispatch claims.
- Physical Android tests must cover permission denial, approximate location, app process death, reboot, battery restrictions, offline delivery, and consent revocation before background sharing is enabled in production.
