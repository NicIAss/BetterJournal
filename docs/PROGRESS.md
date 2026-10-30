# Progress and compatibility

BetterJournal reads the journal sent to your client by the normal game. In singleplayer this comes from the local server; in multiplayer it comes from the server you joined. Existing entries remain the source of recovered lore. BetterJournal adds local viewing history and remembered tapestry pieces.

## Installing in an existing world

You can add BetterJournal to a previously played world or join an existing server with it installed on your client. It requires neither a new world nor newly generated chunks.

| Existing progress | First installation without BetterJournal history |
| --- | --- |
| Some book fragments recovered | All fragments present in the normal journal are readable immediately. Missing fragments stay missing. Their fitted reading-page count depends on window/text size and is separate from recovered-fragment count. |
| Books already read through the vanilla UI | They initially show NEW. The vanilla journal does not provide a reading-position or viewed-page history to import. |
| Tapestry lore already recovered | The received lore restores the matching standard/canonical artwork as complete. This is reconstructed from the journal, rather than proof that the original world assembly still exists. |
| Some tapestry pieces previously looked at without a lore unlock | Those individual views cannot be reconstructed. Look at the intact pieces again while BetterJournal is active. |
| A complete design with no assigned lore | Vanilla has no lore record from which to reconstruct its pieces. Look at those pieces again too. |
| Alternate artwork sharing another design's lore | Only the canonical design is inferred from that lore. The alternate needs direct observations. |

BetterJournal does not scan explored chunks, inventories, collected items or previously visited locations to guess what you saw. It cannot distinguish an old lore grant caused by a different mod from one granted by vanilla. It preserves the journal records it receives.

## While installed

Looking directly at an intact registered tapestry piece records that piece for the current world/player. The observer checks about every fifth of a second while gameplay is active and an open dialog is not holding the mouse. Picking up a piece or merely loading its chunk is not a substitute for looking at it.

Pieces from separate copies of the same design can fill the same remembered image. This creates an artwork collection, not a new lore-unlock system. In vanilla 1.22.7, lore is granted by looking at a complete, correctly assembled, intact tapestry in the world. Recording every piece separately does not call the discovery system or grant its lore. A complete image can therefore have pending lore, and designs with no assigned lore never acquire a story merely by completing the picture.

Rotten observations are ignored. Previously remembered pieces and already received lore are not removed when a tapestry later rots, breaks or is removed; the gallery records past discoveries rather than the current state of a world location.

NEW on books tracks displayed recovered text. A focused, settled page counts as viewed after 1.5 seconds or when the player deliberately leaves a fully displayed page. Animation captures, paused/unfocused pages and skipped text do not count. This does not prove the text was read. Started stories with missing fragments, or an unknown total, keep their bookmark even after all currently recovered text has been viewed.

A completed tapestry can show NEW when its completed detail view has not been acknowledged. A settled detail view clears it; the player does not need to visit every lore page. Partial designs do not prompt completion. Received or changed lore can prompt again.

## Uninstalling and reinstalling

Remove the ZIP while the game is closed, then start the game normally. The vanilla journal returns. BetterJournal registers no blocks or items and does not write server journal entries or discovery records, so removing it does not remove world blocks, items, tapestries or normal lore progress. The same applies in an existing multiplayer world; the server needs no BetterJournal installation.

The local `ModData/betterjournal/` files are retained. The game ignores those files while the mod is absent. Reinstalling in the same profile restores unchanged reading history, bookmarks, acknowledgements and remembered pieces for matching world/player identities.

Normal game lore discoveries continue while BetterJournal is absent. On reinstall, newly received fragments appear unread and newly received tapestry lore can restore the matching standard design. Existing unchanged fragments retain their saved viewing history. Individual pieces only looked at during the absence are not recorded, unless their complete design can later be inferred from received lore. Alternate and no-lore artwork still needs its own observations.

Deleting BetterJournal's entire local folder intentionally resets its extra history. It does not reset vanilla discoveries; known journal chapters remain readable and still reconstruct canonical tapestry artwork.

## Storage

Under the active game profile:

```text
ModData/betterjournal/
  reading.json
  reading.json.bak
  tapestries.json
  tapestries.json.bak
```

`reading.json` stores viewed text ranges, reading anchors, content fingerprints and completed-tapestry acknowledgements. `tapestries.json` stores observed intact piece keys. Both are separated by savegame identifier and player UID. They contain no copies of lore passages and are not synchronized by the server.

History does not automatically follow a world to another computer or game profile. Keep the whole folder, including backups, when moving it; the world/player identifiers must also match. A new savegame or a different player gets separate history, even if the same structures or stories exist there.

Saves are batched and flushed on journal close/world exit. Temporary files are flushed before atomic replacement; `.bak` retains the previous valid snapshot. A damaged primary can recover from backup. Unknown schemas or unrecoverable files are preserved rather than replaced with empty history; progress is then temporary. A crash or abrupt exit can lose the most recent unsaved changes.

## Mods and updates

BetterJournal supports vanilla and modded lore delivered through the normal journal, including directly added entries. Independent lore databases, custom tapestry renderers and other journal replacements are not automatically compatible. Other mods claiming J can compete for the same binding.

The gallery uses received source/localization identities to avoid mixing tapestry lore when numeric chapter IDs change or collide. Duplicate recovered passages display once. Unmatched chapters stay in books. Loaded metadata supplies counts and source identities, not unrecovered reading text.

Reading anchors follow text positions within fragments rather than screen page numbers, so resizing or changing text size can retain progress. Reading identity includes lore code, chapter ID and a fingerprint of source/resolved text; entry IDs and list order are not persistent identities. Added or changed content starts unread. Language changes may show NEW again; returning to an unchanged revision can restore its history.

Removing a lore mod does not necessarily remove its server-saved journal entries. Remaining entries may display localization keys instead of text, or lack tapestry art, if that mod's assets are unavailable. BetterJournal does not recover missing assets or erase those entries. Entries removed from the received journal leave dormant local memories; restoring the same identity and text can restore their reading progress. Changes to codes, IDs, text or translations may prevent that match.

The tested game target is 1.22.7. Major game/API changes and arbitrary mod changes cannot be guaranteed compatible.
