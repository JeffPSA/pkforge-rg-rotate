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

## Target Device Specs
- RG Rotate: 720×720 IPS, 290 PPI, Android 12
- Unisoc T618, 3GB RAM
- Physical: D-pad, face buttons, L/R, analog sticks, touch

## Milestones

### M1: Orientation + Fork Setup — DONE
- SensorLandscape → SensorPortrait
- org.pkforge.app → org.pkforge.mobile
- GitHub Actions CI (auto-build APK on push)

### M2: Home Screen — DONE
- Horizontal shelf → vertical scrollable list
- 3-column cards → vertical stack
- Full-width cartridge rows (icon left, info right)
- Footer condensed to 2 hints

### M3: Box Browser Tabbed Layout — DONE
- 2-column (grid + 330px side panel) → single column + tab switcher
- Tab bar: [prev] GRID | BOX 01 | EDITOR [next]
- Touch: tap Pokémon → auto-switch to editor tab
- Footer: 3 hints (A Select, B Back, + Menu)
- Box manage mode hides tabs

### M4: Editor Screens — DONE
- Kit primitives carry 44dp touch targets
- StatsPopup → single-column rows
- PokedexPicker → 3-column grid
- BankEntryEditor → FlexLayout wrapping
- PadMenu/PickerMenu → taller buttons

### M5: Slide-Up Detail Panel — DONE
- SecondScreenBoxPage → slide-up overlay (ContentPage → Grid)
- Tap Pokémon → panel slides up with name, sprite, stats, types, legality
- Scrim + drag handle + swipe-down dismiss
- ISecondaryDisplayHost removed from MauiProgram.cs

### M6: Polish — NEXT
- Font scaling at 720×720 verification
- Touch targets ≥44dp verification
- Gamepad nav pass
- Test on device, iterate

## Architecture (what to touch and what to leave alone)

### Safe to modify
- `src/PKForge.App/Views/HomePage.cs` — DONE
- `src/PKForge.App/Views/BoxBrowserPage.cs` — DONE
- `src/PKForge.App/Views/Kit.cs` — component primitives
- `src/PKForge.App/Views/DsKit.cs` — design chrome helpers

### Modify with care (have dependencies)
- `src/PKForge.App/MauiProgram.cs` — service registration
- `src/PKForge.App/Views/SecondScreenBoxPage.cs` — needs replacement for M5

### DO NOT MODIFY
- `src/PKForge.Domain/` — contracts/DTOs
- `src/PKForge.Engine/` — PKHeX adapters
- `src/PKForge.AutoMod/` — auto legality
- `src/PKForge.Infrastructure/` — bank, backups
- `src/PKForge.Chrome/` — design tokens (unless mobile-specific tokens needed)
- All editor popup files (StatsPopup.cs, MoveDetailsEditor.cs, etc.) — these are
  already mobile-friendly as overlay popups driven by PadMenu/EditorMenu/PickerMenu,
  which were updated for ≥44dp touch targets above.
- BoxGridRenderer.cs, PartyView.cs

## Build & Deploy
```bash
git clone --recurse-submodules https://github.com/JeffPSA/pkforge-rg-rotate.git
cd pkforge-rg-rotate
dotnet build src/PKForge.App/PKForge.App.csproj -f net10.0-android -c Debug /p:AndroidPackageFormat=apk
```
CI: https://github.com/JeffPSA/pkforge-rg-rotate/actions
