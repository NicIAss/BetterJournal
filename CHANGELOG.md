# Changelog

## 0.1.2 — 2026-11-01

- Fix BetterJournal failing to replace the vanilla journal when an unrelated mod's language asset cannot be loaded, as reported with `stablesurface:lang/en.json`.
- Read tapestry language metadata only from domains that provide loaded tapestry designs, matching the exact language file path.
- Load optional lore and language metadata independently. Missing, unreadable, malformed or empty assets are skipped with a warning instead of interrupting reader setup.
- Keep recovered journal entries readable if unexpected catalogue setup fails; optional fragment totals and tapestry metadata may be unavailable for that session.
- Preserve the selected language's tapestry names with English fallback, mod-qualified names, and specific wildcard matching.
- Add six regression checks for optional asset failures and language fallback; all 144 automated checks pass against Vintage Story 1.22.7.

## 0.1.1 — first public release

- Replace the vanilla journal shortcut with a bookshelf and animated book reader.
- Read vanilla and modded journal entries on fitted pages, with adjustable text size.
- Turn pages with buttons, arrow keys or a horizontal swipe.
- Track recovered story fragments, unread content and per-book reading positions.
- Derive book totals from fragment source identities, including Letters at 5/8, without relying on consistent received chapter numbering.
- Browse discovered tapestry artwork and lore in a separate gallery.
- Remember intact tapestry pieces; keep unseen pieces hidden and ignore rotten observations.
- Match tapestry lore by its source identity and suppress repeated passages.
- Save reading and tapestry progress locally with backup recovery and separate world/player records.
- Document existing-world installation, partial tapestry history, uninstall/reinstall behavior and mod compatibility.

Public name: **BetterJournal**. Author: **NiclAss**. Target: **Vintage Story 1.22.7**. Client-side only.
