# FamilyOS location module

## Implemented capabilities

The location feature now includes the following consent-gated capabilities:

- Android device location permission is while-in-use only and off by default.
- Household sharing is a separate server-side opt-in, also off by default.
- Latest location is visible only to individually authorized active household members. Membership and guardian role never grant access automatically.
- A one-time location fix can be uploaded only when at least one viewer is explicitly authorized.
- Continuous tracking is started from a visible app screen and runs as an Android location foreground service with a persistent notification and Stop action. It does not request background location permission or silently restart after process death or reboot.
- Continuous tracking requires either an explicit viewer grant or the owner's separate personal-history opt-in. The service rechecks both consent and purpose while running and stops when neither remains.
- Personal location history is a separate opt-in. History points are retained for at most 30 days; the API limits each query to seven days and supports deletion.
- Saved places support configurable 50–5,000 meter geofences and arrival/departure alerts. The first fix establishes a baseline rather than generating a false arrival; a 25-meter exit hysteresis reduces GPS drift alerts.
- The family map displays recent shared locations and saved-place circles. It refreshes while open. History routes and derived trip summaries can be displayed for members the viewer is authorized to see.
- Trip summaries are derived from history segments separated by gaps over ten minutes, with a minimum 500-meter path and two-minute duration. They are estimates, not authoritative odometer or driving records.
- Deactivating a household member removes their location record, viewer grants in both directions, history, and geofence presence state.

## Android tracking behavior and limitations

The foreground service updates approximately once per minute while running. Android requires a visible foreground-service notification, and the service must be started while the app is visible. The implementation intentionally uses while-in-use permission rather than `ACCESS_BACKGROUND_LOCATION`. Android may still restrict execution because of battery management, force-stop, process termination, permission changes, or vendor-specific behavior. The app does not silently restart tracking.

The current implementation is not yet validated as a production Life360 replacement. Physical-device validation is required for Android 13–17 notification behavior, screen-off/background operation, Stop action, permission revocation, process death, battery restrictions, location-services-off behavior, and network loss/recovery. There is no bounded offline queue, adaptive movement sampling, battery telemetry, or cross-device service heartbeat.

## API

- `GET /api/location/consent` and `PUT /api/location/consent` — read or change household-sharing consent. Opt-out removes coordinates, viewer grants, and saved history.
- `POST /api/location/current` — upload a fresh location only when sharing is enabled and there is an explicit viewer grant or personal-history consent.
- `GET /api/location/family` — latest fixes explicitly shared with the caller by active household members.
- `GET /api/location/viewers` and `PUT /api/location/viewers/{viewerMemberId}` — manage per-person access. Revoking the final viewer removes stored coordinates when personal history is not enabled.
- `GET /api/location/history/consent` and `PUT /api/location/history/consent` — separate personal-history consent.
- `GET /api/location/history/{targetMemberId}` — authorized history query, defaulting to the last 24 hours; maximum seven-day query window and 30-day retention horizon.
- `DELETE /api/location/history` — delete the current member's stored history without disabling future recording.
- `GET /api/location/trips/{targetMemberId}` — derived trip segments over an authorized history range.
- `GET /api/location/places`, `POST /api/location/places`, `PUT /api/location/places/{placeId}`, and `DELETE /api/location/places/{placeId}` — household saved-place management.

## Privacy and security invariants

- No default-on tracking, silent background collection, or emergency-dispatch claims.
- A check-in request or emergency event must never silently enable location collection.
- A household member sees coordinates only with an active, explicit viewer grant.
- History is separately consented to and can be deleted by its owner.
- Revoking sharing clears coordinates and grants; revoking the last viewer clears coordinates unless the owner independently opted into personal history.
- Notifications for geofence transitions are sent only to members with explicit viewer grants.
- The API validates coordinate bounds, accuracy bounds, timestamp freshness, active family membership, and consent before accepting or returning data.

## Remaining work / release gates

1. Guardian/child consent rules and age-appropriate consent review.
2. User-initiated check-ins with due times, explicit safe responses, overdue state, and no automatic location collection.
3. Explicit SOS workflow with confirmation, emergency-contact configuration, acknowledgment/resolution, optional consented location snapshot, and clear notice that FamilyOS does not contact emergency services.
4. Driving insights based on validated trip data. Crash detection must not be claimed until sensor behavior and false-positive handling are validated on physical devices.
5. Offline handling, battery-aware sampling, location freshness/status, and operational telemetry.
6. Physical Android device tests and end-to-end multi-member authorization tests before production rollout.


## Household check-ins

- A member can request a check-in from another active household member with a due time up to seven days ahead.
- The target receives a notification and can explicitly respond "I'm safe". The requester receives a completion notification.
- The requester can cancel a pending request. Pending requests become "Overdue" in the API response after the due time; the target may still respond.
- Check-in access is limited to the requester and target, and all actions require active household membership.
- A check-in does not enable device location, household sharing, or location history, and does not request a location snapshot.
- This is a lightweight confirmation workflow, not emergency monitoring or emergency services.
- API: `GET /api/checkins`, `POST /api/checkins`, `POST /api/checkins/{id}/respond`, and `POST /api/checkins/{id}/cancel`.
