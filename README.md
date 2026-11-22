# Merged Cells

A grid puzzle game for Android and desktop. The grid is split into regions, mostly long strips. You move a single square, which slides in one direction until it stops at the edge of the map or on the first square of a different region. Get from the start to the end in as few moves as possible.

*Merged Cells* is a working title.

## Status

In development, with playtest builds for Android and Windows. What's in the game now:

- **Campaign:** 75 levels in five chapters: plain sliding, irregular regions, walls, traps and teleporters. Par is worked out by the solver, stars come from how close you get to it, and chapters open as you collect stars.
- **Infinite mode:** generated maps at the difficulty and size you pick, with whichever twists you switch on.
- **Custom levels:** a level editor for making your own, share codes to swap them with friends, and an online level library to publish them and play, like and report other players' levels.
- **Leaderboards:** on each campaign level, how your best compares with everyone else's.
- Undo and redo, restart, zooming and panning on big maps, light and dark themes, and a credits screen.

Every online part (play data for tuning difficulty, leaderboards and the library) is opt-in, asked about separately.

Planned: three chapters that combine the twists (about 120 campaign levels in all), a daily puzzle, a timed mode, a versus mode against a bot, coins to spend on cosmetics, and a Google Play release.

## Controls

- **Android:** swipe to move. Two fingers pan and pinch to zoom.
- **Desktop:** arrow keys or WASD to move, Z or Backspace to undo, Y to redo, R to restart, N for a new map in infinite mode, and + and − (or the mouse wheel) to zoom. Drag with the mouse to pan. Esc goes back.

## Building

You need:

- [Godot 4.7.2 .NET](https://godotengine.org/download) (the .NET build, not the standard one)
- The [.NET 10 SDK](https://dotnet.microsoft.com/download). The projects target .NET 8, but `dotnet test` runs on Microsoft Testing Platform, which needs the .NET 10 SDK.
- For Android exports: JDK 17, the Android SDK and Godot's 4.7.2 export templates, all set in Godot's Editor Settings

```bash
dotnet build MergedCells.sln
dotnet test
```

To play, open `project.godot` in the Godot editor and press Play. The online parts use the Supabase project set in `config/game_config.json`. Without a connection the rest of the game plays as normal.

### Playtest builds

Every push to `main` (except one that changes only the README, LICENSE or PRIVACY.md), or **Run workflow** on the Actions tab, runs the tests and then builds a phones-only APK and a Windows zip. Both go on the repo's `playtest` release, replacing the previous build. The APK is signed with a playtest key stored in two repository secrets, `PLAYTEST_KEYSTORE_BASE64` and `PLAYTEST_KEYSTORE_PASSWORD`, so each new build installs over the last one. [PRIVACY.md](PRIVACY.md) says what each online part keeps and how to have it deleted; its last section is for playtesters.

### Making a level for the campaign

Playtesters are welcome to send in levels. Make one in the level editor, copy its share code, and send it to the developer (on the game's [Discord server](https://discord.gg/YZsNxkhtf4), for example) with a line saying you agree to "Sending a level for the game" in [TERMS.md](TERMS.md) and the name you'd like credited (or that you'd rather not be). If it goes in the game, you're in the credits.

## Project layout

| Folder | Contents |
| --- | --- |
| `src/MergedCells.Core/` | Rules engine, solver, map generator, difficulty rating, saves, level and config parsing, and the server requests. Plain C# with no Godot dependency. |
| `src/MergedCells.Curation/` | The offline tool that generates, climbs and promotes campaign levels (`dotnet run --project src/MergedCells.Curation -c Release -- help`) |
| `tests/MergedCells.Core.Tests/` | xUnit tests for Core and the curation tool. `ShippedLevelsTests` re-solves every level file and checks its stored par. |
| `scenes/` | Godot scenes and scripts: the board, input, menus, the play screen, the level editor and settings |
| `levels/` | One JSON file per campaign level |
| `campaign/chapters.json` | The chapters and their level order |
| `config/game_config.json` | Every tunable number (star thresholds, chapter gates, map sizes, difficulty bands) |
| `themes/` | Colours, styles, fonts and icons |
| `credits/` | The credits and the licence notices the game shows |
| `supabase/` | The server: database migrations, the list of campaign levels and the report email's function |
| `tools/` | Scripts that check the server and install a build on a phone |

## Licence

Copyright © 2026 DefinitelyNotZac

- **Code and everything not listed below:** [GNU General Public License v3.0 or later](LICENSE).
- **Level files (`levels/*.json`) and the app icon (`icon.svg`):** not licensed under the GPL. All rights reserved.
- **Levels players make:** stay their makers'. [TERMS.md](TERMS.md) says what publishing one to the library, or sending one in for the game, allows.
- **Inter font (`themes/fonts/`):** [SIL Open Font License 1.1](themes/fonts/OFL.txt).
