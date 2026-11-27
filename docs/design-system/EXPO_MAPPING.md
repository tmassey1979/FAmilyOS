# Expo → MAUI token mapping

Source of truth for visual parity review. MAUI is now the **sci-fi command** reference; Expo should follow when mobile is refreshed.

## Colors (dark)

| Semantic | Value | MAUI key |
|----------|-------|----------|
| page bg | `#0A0E14` | `BgPage` |
| surface | `#121A2B` | `BgSurface` |
| surface alt | `#1A2438` | `BgSurfaceAlt` |
| border | `#2A3A55` | `BorderSubtle` |
| primary | `#22D3EE` | `AccentPrimary` |
| secondary | `#A78BFA` | `AccentSecondary` |
| glow | `#67E8F9` | `AccentGlow` |
| text | `#F1F5F9` | `TextPrimary` |
| text muted | `#94A3B8` | `TextMuted` |
| danger | `#FB7185` | `AccentDanger` |
| success | `#34D399` | `AccentSuccess` |
| warning | `#FBBF24` | `AccentWarning` |

Light theme uses cooler slate pages with deeper cyan (`#0891B2`) for outdoor readability.

## Spacing

| Token | Value |
|-------|-------|
| `SpaceXs` | 4 |
| `SpaceSm` | 8 |
| `SpaceMd` | 16 |
| `SpaceLg` | 24 |
| `SpaceXl` | 32 |
| `ControlHeightMin` | 52 |
| `RadiusMd` | 14 |
| `RadiusLg` | 18 |

## Components

| Concept | MAUI control |
|---------|--------------|
| Button | `FoButton` (Primary / Secondary / Danger / Ghost / Accent) |
| Card | `FoCard` (+ Tone: Default / Accent / Success / Warning / Danger) |
| Badge | `FoBadge` |
| EmptyState | `FoEmptyState` |
| SectionTitle | `FoSectionTitle` |
