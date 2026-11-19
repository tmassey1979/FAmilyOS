# Adding a new screen

1. Page background via theme (`BgPage`).
2. Text via `TextTitle` / `TextHeading` / `TextBody` / `TextCaption` styles.
3. Spacing via `SpaceSm` / `SpaceMd` / `SpaceLg` only.
4. Actions via `FoButton` — never hex `BackgroundColor`.
5. Groups via `FoCard`.
6. Status via `FoBadge` / `FoStatusBanner`.
7. Empty / error / offline via shared state controls.
8. Accessibility names on every interactive control.
9. New control variants must appear on `DesignSystemGalleryPage`.
10. No API, auth, or product behavior changes in design-system PRs.
