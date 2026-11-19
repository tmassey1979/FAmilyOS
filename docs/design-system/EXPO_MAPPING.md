# Expo → MAUI token mapping

Source of truth for visual parity review. Expo values from `mobile/src/theme.ts` and `mobile/src/components/ui.tsx`.

## Colors

| Expo (`theme.ts`) | Value | MAUI semantic | MAUI dark raw |
|-------------------|-------|---------------|---------------|
| `colors.bg` | `#0F172A` | `BgPage` (Dark) | `PaletteNavy950` |
| `colors.surface` | `#1E293B` | `BgSurface` (Dark) | `PaletteSlate800` |
| `colors.surfaceAlt` | `#334155` | `BgSurfaceAlt` (Dark) | `PaletteSlate700` |
| `colors.border` | `#475569` | `BorderSubtle` (Dark) | `PaletteSlate600` |
| `colors.primary` | `#38BDF8` | `AccentPrimary` | `PaletteSky400` |
| `colors.text` | `#F8FAFC` | `TextPrimary` (Dark) | `PaletteSlate50` |
| `colors.textMuted` | `#94A3B8` | `TextMuted` (Dark) | `PaletteSlate400` |
| `colors.danger` | `#F87171` | `AccentDanger` | `PaletteRed400` |
| `colors.success` | `#4ADE80` | `AccentSuccess` | `PaletteGreen400` |
| `colors.warning` | `#FBBF24` | `AccentWarning` | `PaletteAmber400` |

Light theme is an **evolution** for daytime readability (cream page, navy text) while keeping the same accent and status colors.

## Spacing

| Expo | Value | MAUI |
|------|-------|------|
| `spacing.xs` | 4 | `SpaceXs` |
| `spacing.sm` | 8 | `SpaceSm` |
| `spacing.md` | 16 | `SpaceMd` |
| `spacing.lg` | 24 | `SpaceLg` |
| `spacing.xl` | 32 | `SpaceXl` |

## Components

| Expo (`ui.tsx`) | MAUI control |
|----------------|--------------|
| `Button` | `FoButton` |
| `Card` | `FoCard` |
| `Badge` | `FoBadge` |
| `EmptyState` | `FoEmptyState` |
| `SectionTitle` | `FoSectionTitle` |
