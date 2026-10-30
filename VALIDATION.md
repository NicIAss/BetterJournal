# Validation — BetterJournal v0.1.0

Target: Vintage Story **1.22.7**, .NET 10. Prepared for the first public release; final player testing is pending. Version numbering was reset from private development versions without changing the progress schema.

## Automated checks

The release build passes all **137 checks** against the installed 1.22.7 game assemblies and assets, with zero compiler warnings/errors. Coverage includes:

- Vanilla and arbitrary mod-added journal entries, localization, rich text, page breaks and updates.
- Fitted pagination with complete word retention and formatting/link preservation.
- Book opening/closing, rigid board bounds, page-turn face selection and smooth window geometry.
- Bounded layout reuse, invalidation, cache eviction and long-text fitting workload.
- Reading anchors, unread fragments, deliberate departure, focus/pause exclusions and bookmark lifecycle.
- Atomic progress writes, backup recovery, unknown schemas, interrupted migration and world/player isolation.
- All 31 vanilla tapestry definitions, individual pieces, rotten observations and hidden alternate artwork.
- Source-based tapestry routing, duplicate suppression, default/reordered/colliding IDs and full/incremental protobuf journal messages.
- Specific Rotbeast naming rather than the broader Rot wildcard, plus mod-domain/exact translation precedence.
- BetterJournal name migration from development ModData, backup-only discoveries, authoritative newer saves and older ModConfig fallback.
- Existing-world installation, no retroactive partial/alternate observations, complete remembered artwork without a new lore grant, uninstall/reinstall storage round trips, discoveries received while absent, dormant reading history and a local-only progress reset.

Game files are used locally for checks and are not packaged or uploaded. The normal player profile/worlds are not used by the automated suite.

## Current status and limits

The underlying UI was exercised during development at multiple GUI sizes, including book controls, swipes, cover/page animations, text-size changes, bookmarks and tapestry prompts. The author-supplied screenshots document the current experience, including a correctly separated Forlorn Hope lore entry.

The last automated tapestry regressions reproduce the duplicate/mismatched-chapter conditions with installed game types and serialization. They do not prove that the author's specific multiplayer server has been retested. BetterJournal's new name, icon, progress migration and final package still need the requested in-game pass before publication. The checklist is in [docs/RELEASING.md](docs/RELEASING.md).

The mod remains client-side and reads the game's received journal. The server journal and discovery records are never rewritten. Local saves retain schema version 1. Unknown mod lore stays accessible; missing lore is never read from the catalogue into the UI.

A first fit of long content or a new text size can still hitch on the game thread. Reusing layouts and existing text/control elements reduces repeated work; this is not a guarantee of flat frame times.

## Description review

The README, release notes, metadata summary and prepared ModDB page use plain feature sentences. Existing-world and uninstall claims were checked against the installed vanilla tapestry discovery code, the client integration and the local storage/projection code. No runtime discovery or uninstall behavior needed changing for these documented scenarios.

Six lifecycle regression checks exercise the projections and actual local file save/load operations; they do not simulate a complete running server or prove all third-party mod combinations compatible. The ModDB preview was checked in a browser at its default desktop width: all four supplied screenshots and three button icons loaded, the page had no horizontal overflow, and collapsible sections opened/closed. The actual ModDB editor/sanitizer and a narrow viewport were not tested. Public screenshot URLs depend on the currently private GitHub repository becoming public at release time.
