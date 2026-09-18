# PKForge-Mobile Project Plan

## End Goal
Mobile-friendly fork of PKForge for RG Rotate (720×720 square, Android 12).
Touch-first, single-screen, tabbed UI. All core save-edit + bank functionality preserved.

## Decision Log
- **Navigation**: Touch-first with gamepad as bonus (not primary)
- **Second screen**: Slide-up detail panel on tap instead of separate display
- **Box browser**: Tabbed (grid view ↔ editor view)
- **Visual**: Keep pixel aesthetic, recompose layout only
- **Engine layer**: Untouched — Domain/Engine/AutoMod/Infrastructure reused as-is

## Milestones

### M1: Orientation + Fork Setup — IN PROGRESS
- [x] Clone repo
- [ ] Rename project (PKForge → PKForge.Mobile / "PKForge Mobile")
- [ ] Change orientation: SensorLandscape → SensorPortrait
- [ ] Update app ID, name, icons
- [ ] Set up build pipeline (no .NET SDK locally)
- [ ] Verify clean build

### M2: Home Screen Recomposition
- [ ] Vertical cartridge list (was horizontal shelf)
- [ ] 2×2 or vertical destination cards (was 3-column)
- [ ] Condensed footer hint bar

### M3: Box Browser — Tabbed Layout
- [ ] Tab 1: Box grid (square-optimized slot layout)
- [ ] Tab 2: Editor (was 330px side panel)
- [ ] Touch-friendly slot sizing (≥44dp)

### M4: Editor Screens
- [ ] Stats, moves, met origin → stacked collapsible sections
- [ ] Larger touch targets

### M5: Slide-Up Detail Panel
- [ ] Inline summary replaces SecondScreenBoxPage
- [ ] Tap Pokémon → slide-up panel with stats, sprite, legality
- [ ] Gamepad D-pad navigates grid; A opens slide-up

### M6: Polish
- [ ] Font scaling verification at 720×720
- [ ] Gamepad nav pass (D-pad + A/B/X/Y still work)
- [ ] Full regression: open save, edit, legalize, bank, backup

## Target Device Specs
- RG Rotate: 720×720 IPS, 290 PPI, Android 12
- Unisoc T618, 3GB RAM
- Physical: D-pad, face buttons, L/R, analog sticks, touch

## Build
```bash
git submodule update --init --recursive
dotnet build src/PKForge.App/PKForge.App.csproj -f net10.0-android
```
