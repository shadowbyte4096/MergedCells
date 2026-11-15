# Merged Cells

A grid puzzle game for Android and desktop. The grid is split into regions, mostly long strips. You move a single square, which slides in one direction until it stops at the edge of the map or on the first square of a different region. Get from the start to the end in as few moves as possible.

*Merged Cells* is a working title.

## Status

Early development. What works now: playing a level, with 10 hand-made levels, par worked out by the solver, star ratings, unlimited undo, restart, and light and dark themes.

Planned: a campaign with twists (walls, traps, teleporters), an infinite mode with generated maps, a daily puzzle, a timed mode and a versus mode.

## Building

You need:

- [Godot 4.7.2 .NET](https://godotengine.org/download) (the .NET build, not the standard one)
- The [.NET 10 SDK](https://dotnet.microsoft.com/download). The projects target .NET 8, but `dotnet test` runs on Microsoft Testing Platform, which needs the .NET 10 SDK.
- For Android exports: JDK 17, the Android SDK and Godot's 4.7.2 export templates, all set in Godot's Editor Settings

```bash
dotnet build MergedCells.sln
dotnet test
```

To play, open `project.godot` in the Godot editor and press Play. On desktop, move with the arrow keys or WASD. On Android, swipe.

### Playtest builds

Every push to `main` (except README or LICENSE changes), or **Run workflow** on the Actions tab, runs the tests and then builds a phones-only APK and a Windows zip. Both go on the repo's `playtest` release, replacing the previous build. The APK is signed with a playtest key stored in two repository secrets, `PLAYTEST_KEYSTORE_BASE64` and `PLAYTEST_KEYSTORE_PASSWORD`, so each new build installs over the last one.

## Project layout

| Folder | Contents |
| --- | --- |
| `src/MergedCells.Core/` | Rules engine, solver, and level and config parsing. Plain C# with no Godot dependency. |
| `tests/MergedCells.Core.Tests/` | xUnit tests. `ShippedLevelsTests` re-solves every level file and checks its stored par. |
| `scenes/` | Godot scenes and scripts: the board, input and the play screen |
| `themes/` | Colours, styles, fonts and icons |
| `levels/` | One JSON file per level |
| `config/game_config.json` | Every tunable number (star thresholds, rewards, map sizes) |

## Licence

Copyright © 2026 DefinitelyNotZac

- **Code and everything not listed below:** [GNU General Public License v3.0 or later](LICENSE).
- **Level files (`levels/*.json`) and the app icon (`icon.svg`):** not licensed under the GPL. All rights reserved.
- **Inter font (`themes/fonts/`):** [SIL Open Font License 1.1](themes/fonts/OFL.txt).
