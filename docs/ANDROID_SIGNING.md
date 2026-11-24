# Android release signing (in-place upgrades)

FamilyOS MAUI APKs must be signed with a **stable release keystore** so Android allows installing a newer `versionCode` over an existing install without uninstalling.

## GitHub Actions secrets

Add these under **Settings → Secrets and variables → Actions**:

| Secret | Value |
|--------|--------|
| `ANDROID_KEYSTORE_BASE64` | Base64 of the `.jks` file (`base64 -w0 familyos-release.jks`) |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias (default: `familyos`) |
| `ANDROID_KEY_PASSWORD` | Key password |

The coordinated release workflow decodes the keystore and signs with:

```
-p:AndroidKeyStore=true
-p:AndroidSigningKeyStore=...
-p:AndroidSigningStorePass=...
-p:AndroidSigningKeyAlias=...
-p:AndroidSigningKeyPass=...
```

## One-time migration

Builds **before** release signing used a debug/ephemeral key. Android will **reject** an upgrade from those to a release-signed APK.

1. Uninstall the old FamilyOS build once.
2. Install the first **release-signed** APK.
3. All later signed builds with a higher `versionCode` (CI run number) should upgrade in place.

## Back up the keystore

If the keystore is lost, users must uninstall to install a new signing key. Keep a private offline copy of the `.jks` and passwords.
