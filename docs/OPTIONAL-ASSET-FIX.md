# Optional catalogue asset loading regression

Included in **v0.1.2**, dated 2026-11-01. See [release notes](releases/v0.1.2.md).

## Report and cause

The supplied Vintage Story 1.22.7 client log reports an exception in `LoreCatalogue` during `LevelFinalize`:

```text
Requested asset [stablesurface:lang/en.json] could not be found
AssetManager.GetMany<T> -> LoreCatalogue..ctor -> BetterJournalSystem.Install
```

The old constructor deserialized every mod's English/current language asset to resolve tapestry wildcard names. A backing-file load failure escapes the game's bulk loader, before BetterJournal installs the journal hotkey handler. The player consequently sees the vanilla journal.

Stable Surface 0.0.3 changes temporal stability and has no lore or tapestry content. Its archive has a language file under `assets/StableSurface/lang/en.json`; its presence in the asset index does not guarantee that the client can reload it from the selected origin. The fix does not depend on diagnosing or changing that mod's packaging or server asset transfer.

## Changed behavior

- Enumerate optional lore metadata locations, load each independently, and warn/continue after missing files, invalid JSON or null data.
- Load tapestry language metadata only for domains represented by loaded tapestry designs, matching the exact locale asset path.
- Preserve English/current locale precedence, mod-qualified names, and the most specific wildcard match (Rotbeast versus Rot).
- If unexpected catalogue setup still fails, log the exception and install the reader with empty optional metadata. Received journal entries remain readable with unknown totals; optional tapestry metadata is unavailable for that session.

No changes to vanilla journal discovery, networking or stored journal data. No dummy language file, dependency on Stable Surface, or changes to other mods are required.

## Validation

Release build against installed 1.22.7 assemblies: zero warnings/errors; **144 checks passed** (138 existing, six new regression checks). New checks cover isolated load failures, malformed/null metadata, unrelated server-only domains, exact locale paths, a failed relevant language origin, asset-index failure, full constructor locale fallback, and readable received entries with an empty catalogue.

Use an isolated game profile for the modpack smoke test. Test profile settings, account information, game binaries, other authors' mods, worlds and logs belong under ignored `work/` and must never be included in a source commit or mod release.

The first in-game smoke test on 2026-11-01 reached `Journal reader enabled`, opened the bookshelf with J, and displayed all 31 vanilla tapestry designs. There was no BetterJournal warning or reported LevelFinalize exception. The local profile has 89/90 requested mods; its ignored test notes and installed manifest record the unavailable custom straw mod and three version differences. This does not replace retesting on the reporter's original server.
