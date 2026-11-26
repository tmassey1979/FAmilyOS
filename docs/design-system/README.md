# FamilyOS Design System (MAUI)

Single source of truth for the .NET MAUI client visual language.

**Parent issue:** [#30](https://github.com/tmassey1979/FAmilyOS/issues/30)  
**Brand direction:** midnight sci-fi, ion cyan, restrained ultraviolet, calm and effortless interaction.

## Contents

| Doc | Purpose |
|-----|---------|
| [PRINCIPLES.md](PRINCIPLES.md) | Experience principles and visual direction |
| [TOKENS.md](TOKENS.md) | Colors, type, spacing, radii, elevation, motion |
| [COMPONENTS.md](COMPONENTS.md) | Shared controls, variants, states |
| [EXPO_MAPPING.md](EXPO_MAPPING.md) | Legacy Expo → MAUI mapping |
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

1. **No raw colors or spacing in pages** — use semantic resources.
2. **Semantic names only** — use meaning such as `TextPrimary`, not palette names.
3. **Every interactive control** exposes an accessibility name/state.
4. **Loading / empty / error / offline / denied** states are first-class.
5. Keep screens simple; the design system should make consistency easy rather than force developers to hand-style each page.
