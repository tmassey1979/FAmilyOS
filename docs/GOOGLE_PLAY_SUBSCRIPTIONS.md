# Google Play subscriptions and onboarding

## Product catalogue

FamilyOS requires the Core subscription. The household owner can add either optional module or both:

| Product | Monthly | Annual |
| --- | ---: | ---: |
| Core | $9.99 | $89.00 |
| Location | $4.99 | $39.00 |
| Finance software | $5.99 | $49.00 |
| Location + Finance bundle | $8.99 | $79.00 |

The bundle replaces the two individual add-ons; it is not stacked on top of them. Core plus both modules is therefore $18.98/month or $168/year. Core plus Location is $14.98/month or $128/year. Core plus Finance is $15.98/month or $138/year.

There is no free-forever tier. The currently documented 30-day trial applies to Core. Optional modules are not part of the trial unless a separate Play offer explicitly includes them.

## Android purchase provider

Google Play Billing is the only in-app purchase provider in this Android implementation. Stripe is out of scope. Do not enable checkout until the corresponding subscriptions and base plans have been created in Play Console and their exact product/base-plan/offer identifiers are supplied through deployment configuration.

The mobile client may launch a Play purchase flow and submit the resulting purchase token, but it must never grant entitlements itself. The API must verify purchase tokens with the Google Play Developer API using server-held credentials, persist the verified purchase and derive the household's effective entitlements from verified state.

## Entitlement model

- Core is mandatory for an active paid household.
- Location and Finance are independent optional entitlements.
- The Location + Finance bundle grants both optional entitlements and replaces separate add-on purchases.
- Entitlements belong to the household subscription owner and are shared with active household members according to household membership/authorization rules.
- Feature availability, role authorization and member consent are separate checks. A paid Location entitlement must never enable location sharing without each member's explicit, revocable consent.
- Expired, refunded, revoked or otherwise invalid purchases must remove the corresponding entitlement after server verification and lifecycle processing.
- Support restore purchases, renewals, cancellation, grace period, billing retry, refunds/revocations and expiration. Cancellation does not immediately remove access while the verified paid period remains active.

## Onboarding sequence

1. Authenticate the user.
2. Present the Core plan and monthly/annual billing choices, with the 30-day Core trial terms clearly visible when eligible.
3. Allow the owner to select Location, Finance, or the combined add-on bundle.
4. Display the full recurring price, trial scope, renewal date/terms and selected modules before opening Google Play Billing.
5. Complete and verify the purchase with the API.
6. Only after the API confirms the household entitlement, continue to household creation or joining.
7. Existing subscribers can restore/refresh purchases and continue without purchasing again.

Do not create a household and show paid modules as active while purchase verification is pending or has failed. Provide a recoverable retry path.

## Settings and family reset

The existing Settings page already exposes an owner-only, double-confirmed **Reset family data** action. Keep that action. The server-side reset must erase household-owned records and detach the user's current household membership, while preserving:
- the user's authentication account and identity-provider account;
- verified Google Play purchase records, subscription ownership and billing audit history;
- the active subscription and effective entitlements, unless the owner separately cancels the subscription through Google Play.

After reset, the authenticated user returns to onboarding and can create a new household or join an existing one. Subscription ownership/transfer rules must be enforced server-side; a reset must not silently cancel or discard a paid subscription.

## Required configuration before live purchases

- Google Play Console subscription product IDs, base-plan IDs and offer IDs for monthly/annual Core, Location, Finance and bundle products.
- Core trial offer configuration (30 days) and eligibility rules.
- Google Play Developer API service-account credentials stored as server secrets with least privilege.
- Play package name and service-account access to the app's subscriptions.
- License testers and internal testing track configuration.

Never commit service-account JSON, private keys, purchase tokens or other billing secrets. Never invent product identifiers or assume Play Console products exist.

## Release gates

- Unit/integration tests prove invalid tokens cannot grant entitlements.
- Tests cover household sharing, restore, renewals, cancellation, grace period, billing retry, refund/revocation, expiration and trial eligibility.
- Tests prove reset preserves authentication and verified subscription ownership while deleting household data.
- Google Play license-test purchases pass on an app installed from an eligible Play test track.
- Legal terms, privacy policy, support/refund contact and clear renewal disclosures are ready before commercial activation.
