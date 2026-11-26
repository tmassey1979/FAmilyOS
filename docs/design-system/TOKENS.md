# Design tokens

All values live in MAUI resource dictionaries under `src/FamilyOS.Maui/Resources/`. Pages and shared controls must use semantic resources rather than hard-coded colors.

## Semantic colors (theme-aware)

| Resource | Light | Dark |
|----------|-------|------|
| `BgPage` | `#F4F7FC` | `#030712` |
| `BgSurface` | `#FFFFFF` | `#0A1020` |
| `BgSurfaceAlt` | `#EAF0F8` | `#0E1A2E` |
| `BgGlow` | `#E5F8FF` | `#071A2C` |
| `BorderSubtle` | `#D7E1EF` | `#1B3552` |
| `TextPrimary` | `#101A2B` | `#EAF6FF` |
| `TextMuted` | `#60718A` | `#8CA7C5` |
| `TextOnAccent` | `#04101A` | `#04101A` |
| `AccentPrimary` | `#008DA8` | `#45F3FF` |
| `AccentSecondary` | `#6D5CE7` | `#B18CFF` |
| `AccentDanger` | `#D83D5B` | `#FF5D7D` |
| `AccentSuccess` | `#087F61` | `#47F0B2` |
| `AccentWarning` | `#956000` | `#FFC66D` |

## Spacing and geometry

`SpaceXs` 4 · `SpaceSm` 8 · `SpaceMd` 16 · `SpaceLg` 24 · `SpaceXl` 32 · `ControlHeightMin` 48 · `RadiusMd` 12 · `RadiusLg` 16 · shared card radius 22.

## Rules

- Use semantic resources in every page and reusable control.
- Keep touch targets at least 48dp and body text comfortably readable.
- Accent cyan is for primary focus; violet is a secondary highlight, never a second competing primary.
- Status colors communicate status only. Do not use color as the only state indicator.


## Component treatment

- Primary buttons are high-contrast ion-cyan with dark text; secondary actions sit on a quieter raised surface.
- Cards use a 22dp radius, fine semantic border, and restrained surface contrast rather than heavy shadows.
- Inputs use the surface color, readable 16sp text, muted placeholder text, and at least 48dp height.
- Status color is reserved for actual success, warning, danger, and information—not decoration.
