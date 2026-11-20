# FamilyOS Design System (MAUI)

Single source of truth for the .NET MAUI client visual language.

**Parent issue:** [#30](https://github.com/tmassey1979/FamilyOS/issues/30)  
**Brand baseline:** Expo `mobile/src/theme.ts`, `mobile/src/components/ui.tsx`, navy `#0F172A` + sky accent `#38BDF8`.

## Contents

| Doc | Purpose |
|-----|---------|
| [PRINCIPLES.md](PRINCIPLES.md) | Visual direction and product tone |
| [TOKENS.md](TOKENS.md) | Colors, type, spacing, radii, elevation, motion |
| [COMPONENTS.md](COMPONENTS.md) | Shared controls, variants, states |
| [EXPO_MAPPING.md](EXPO_MAPPING.md) | Expo → MAUI token map for parity review |
| [ADDING_A_SCREEN.md](ADDING_A_SCREEN.md) | How to add UI without hard-coded literals |

## Code locations

| Path | Role |
|------|------|
| `src/FamilyOS.Maui/Resources/Styles/` | Color palette, semantic tokens, typography, spacing, control styles |
| `src/FamilyOS.Maui/Resources/Themes/` | Light / dark theme dictionaries |
| `src/FamilyOS.Maui/Controls/` | Reusable components (`Fo*` prefix) |
| `src/FamilyOS.Maui/Services/ThemeService.cs` | Runtime theme preference |
| `src/FamilyOS.Maui/Pages/DesignSystemGalleryPage.xaml` | Component showcase |
| `tests/FamilyOS.Maui.Tests/` | Theme resource availability tests |

## Rules

1. **No raw colors or spacing in pages** — use `{StaticResource ...}` / `{AppThemeBinding ...}`.
2. **Semantic names only** — `TextPrimary`, not `Slate50`.
3. **Every interactive control** exposes accessibility name/state.
4. **Loading / empty / error / offline / denied** states are first-class, not afterthoughts.
5. Expo remains the visual reference until MAUI parity is accepted; do not restyle Expo as a prerequisite.
