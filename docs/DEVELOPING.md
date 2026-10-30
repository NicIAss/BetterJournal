# Building BetterJournal

Requires PowerShell, the .NET 10 SDK, and an installed copy of Vintage Story **1.22.7**. Game assemblies are referenced locally and must not be committed or uploaded.

```powershell
./build.ps1 -GamePath 'C:\Games\VintageStory'
```

Alternatively, set `VINTAGE_STORY` to the game directory. Use `-DotnetPath` when the SDK executable is not on PATH. The build script compiles Release, runs the compatibility checks, and writes `dist/betterjournal-0.1.0.zip` and its SHA-256 checksum. It packages only the mod DLL, metadata, icon, localization and license notices.

Run checks separately:

```powershell
dotnet run --project tests/BetterJournal.Tests.csproj -c Release -p:GamePath='C:\Games\VintageStory'
```

Some checks inspect the installed game assets and assemblies, including all vanilla tapestry definitions and the game's protobuf serializer. An ordinary hosted runner without the game installation cannot run the complete build. There is no workflow that downloads or republishes proprietary game files.

## Structure

- `BetterJournalSystem.cs`: client integration, journal shortcut, world lifecycle and progress storage.
- `BookshelfDialog*.cs`: bookshelf, book and tapestry views.
- `BookMotion.cs`, `BookPose.cs`, `WindowMotion.cs`: animated books and window transitions.
- `JournalBooks.cs`, `BookPagination.cs`, `PageLayoutCache.cs`: received lore projection and fitted pages.
- `LoreCatalogue.cs`, `TapestryCollection.cs`: lore identity, mosaics, names and gallery routing.
- `ReadingProgress.cs`, `ReadingExposure.cs`, `SavedDataFile.cs`, `LegacyProgress.cs`: reading history, visibility rules, atomic files and migration.
- `tests/`: compatibility, pagination, animation, persistence, migration and tapestry regression checks.

The integration reads the private `ModJournal.ownJournal` field because the targeted game does not expose a public client-journal getter. It retains the normal discovery/networking systems and restores the previous journal hotkey handler when leaving a world. If integration is unavailable, the normal handler is retained.

Use a separate game data profile for graphical testing. Keep test worlds, logs, account settings, game libraries and build outputs out of commits. `work/`, `dist/`, `bin/` and `obj/` are ignored.
