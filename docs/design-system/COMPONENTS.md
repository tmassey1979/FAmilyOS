# Shared components (`Fo*`)

All controls live under `src/FamilyOS.Maui/Controls/` and consume **semantic theme resources only** (no raw hex in pages).

| Control | Purpose | Variants / states |
|---------|---------|-------------------|
| `FoButton` | Primary actions | Primary, Secondary, Danger, Ghost; Loading; Disabled |
| `FoCard` | Surface container | Default elevated surface + subtle border |
| `FoBadge` | Status / count pill | Neutral, Info, Success, Warning, Danger |
| `FoChip` | Filter / tag | Neutral, Info, Success, Warning, Danger; Selected |
| `FoAvatar` | Member mark | Initials; configurable size |
| `FoSectionTitle` | Section caption | Uppercase muted label |
| `FoEmptyState` | Zero-data | Title, subtitle, optional action |
| `FoStatusBanner` | Inline alert | Info, Success, Warning, Danger, Offline |
| `FoListRow` | Dense list item | Title, subtitle, trailing slot |
| `FoTextField` | Form input | Label, helper, error |
| `FoLoadingView` | In-progress | Message |
| `FoErrorView` | Recoverable failure | Message + retry |
| `FoAppBar` | Top navigation chrome | Title, subtitle, leading back, trailing slot |
| `FoDialog` | Modal confirm / alert | Title, message, body slot, primary/secondary, scrim dismiss |
| `FoSheet` | Bottom sheet | Title, subtitle, body slot, actions, scrim dismiss |

## Accessibility

- Interactive controls set `SemanticProperties.Description` (and Hint for loading/selected).
- Minimum touch target height is `ControlHeightMin` (48).
- Theme changes via `IThemeService` update semantic keys without app restart.

## Gallery

Open `DesignSystemGalleryPage` to exercise every variant in light and dark.
