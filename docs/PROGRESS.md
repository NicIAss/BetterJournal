# Reading and discovery progress

The normal server journal remains the source of recovered lore. BetterJournal stores only local viewing history and observed tapestry pieces.

Under the active game profile:

```text
ModData/betterjournal/
  reading.json
  reading.json.bak
  tapestries.json
  tapestries.json.bak
```

`reading.json` contains viewed text ranges, reading anchors, fingerprints and completed-tapestry acknowledgements. `tapestries.json` records intact tapestry pieces seen. Both are separated by savegame identifier and player UID. A custom game data profile gets its own files.

A focused, settled reading page counts as viewed after 1.5 seconds, or when the player deliberately leaves a fully displayed page. Animation captures, paused/unfocused pages and skipped content do not count. This records display exposure rather than proving the text was read.

Reading anchors follow positions within fragments rather than page numbers, so changing text size or window size can retain the reading position. Fragment identity includes lore code, chapter ID and a fingerprint of source/resolved text. Numeric entry IDs and list order are not persistent identities. Added or changed content starts unread. Removed content leaves dormant history; restoring the same identity and text can restore progress. Translation changes may show NEW again. Uncatalogued entries have an unknown total.

Saves are batched and flushed on journal close/world exit. Temporary files are flushed before atomic replacement; the previous valid snapshot is retained as `.bak`. Damaged primaries can recover from backup. Unknown schemas or unrecoverable files are retained rather than replaced with empty progress. The latest unsaved exposure can be lost in a crash. Local progress does not travel automatically between computers.

## Earlier development builds

Lore Bookshelf development saves under `ModData/lorebookshelf/` are imported before older `ModConfig/lorebookshelf-*.json` files. Existing BetterJournal data always takes precedence. Valid imported data is written and read back, with an active backup, before original files move into `legacy-lorebookshelf/` or `legacy-modconfig/` beneath the new folder. Archive name collisions preserve both copies. Interrupted cleanup retries on later loads. A backup-only save can migrate too.

The mod ID change does not change world/player keys, fragment identities or the saved-data schema. Remove the old development mod before using BetterJournal; do not load both journal replacements together. Keep the whole progress folder, including backups and archives, when moving profiles.
