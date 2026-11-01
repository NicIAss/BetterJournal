# Releasing BetterJournal

Current version: **v0.1.2**, mod ID `betterjournal`, author **NiclAss**, target game **1.22.7**. Release notes: [v0.1.2](releases/v0.1.2.md).

GitHub releases are published manually; there is no automatic publishing workflow. Version 0.1.2 is the optional catalogue asset loading fix. The older v0.1.1 GitHub draft is separate and should not be republished as part of this update. Uploading a GitHub release does not update the [Vintage Story Mod DB page](https://mods.vintagestory.at/betterjournal).

## Validation and package

1. Set the same version in `modinfo.json` and `BetterJournal.csproj`.
2. Run `build.ps1` against Vintage Story 1.22.7. For v0.1.2, all 144 checks must pass with zero compiler warnings/errors.
3. Verify the ZIP contains the tested DLL, matching version metadata, assets and license notices. Exclude game libraries, test profiles, logs, credentials and other authors' mod archives.
4. Verify the SHA-256 file against the final ZIP. Keep the standard names `BetterJournalv0.1.2.zip` and `BetterJournalv0.1.2.zip.sha256`.
5. Review the README, changelog, release notes and validation record so that claims match the tests actually performed.

## In-game checks

- Confirm the installed mod list shows BetterJournal 0.1.2, NiclAss and the selected screenshot icon.
- Open/close J, switch Books/Tapestries and return with Back/Escape.
- With Stable Surface or another mod lacking usable client language metadata, confirm BetterJournal enables without the reported `LevelFinalize` exception.
- Open a vanilla and a modded book; check buttons, swiping, animations and A-/A+.
- Read the final available text; confirm NEW clears, the bookmark behaves correctly and reopening resumes at unread content/the saved position.
- Check Nightfall, Rot, Rotbeast, Schematic D, Blackguard Map and Forlorn Hope for correct artwork, names and a single correct lore passage.
- Verify partial/rotten tapestry pieces and completed NEW prompts.
- Leave/rejoin the multiplayer server and confirm progress remains correct.
- Confirm local progress is stored under `ModData/betterjournal/` and persists after restarting.
- When changing game language, restart before checking translated content and English fallback.


The v0.1.2 smoke test covered the bookshelf, gallery and client restart in an isolated modpack. It did not repeat the entire gameplay checklist or retest the reporter's original multiplayer server. See [VALIDATION.md](../VALIDATION.md).

## GitHub publication

After user approval to publish, commit the reviewed source/documentation, update `main`, and create tag `v0.1.2` at that commit. Push `main` and the tag without rewriting existing history or tags.

Create a draft release for the existing tag using `docs/releases/v0.1.2.md` as the notes. Upload the ZIP and checksum, verify the uploaded assets, then publish it as the latest stable release. GitHub supplies source archives for the tag. Confirm the published release points to the expected tag and has both assets attached.

The prepared ModDB fragment is `docs/moddb/description.html`, with a short summary and local preview beside it. It uses NiclAss's supplied page design. Its screenshot URLs use the public `NicIAss/niclass-images/betterjournal/` folder, separately from this code repository. The preview uses those same public image URLs. ModDB publication requires its own explicit task; do not claim its version changed from a GitHub upload alone.
