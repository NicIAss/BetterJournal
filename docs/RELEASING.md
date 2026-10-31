# First release preparation

Current version: **v0.1.1**, mod ID `betterjournal`, author **NiclAss**, target game **1.22.7**.

The repository is public and version 0.1.1 is live on the [Vintage Story Mod DB](https://mods.vintagestory.at/betterjournal). Tag `v0.1.1` identifies the build. The GitHub release remains a draft; publication there is a separate manual step. There is no automatic publishing workflow.

## Final in-game pass

- Confirm the installed mod list shows BetterJournal 0.1.1, NiclAss and the selected screenshot icon.
- Open/close J, switch Books/Tapestries and return with Back/Escape.
- Open a vanilla and a modded book; check buttons, swiping, animations and A-/A+.
- Read the final available text; confirm NEW clears, the bookmark behaves correctly and reopening resumes at unread content/the saved position.
- Check Nightfall, Rot, Rotbeast, Schematic D, Blackguard Map and Forlorn Hope for correct artwork, names and a single correct lore passage.
- Verify partial/rotten tapestry pieces and completed NEW prompts.
- Leave/rejoin the multiplayer server and confirm progress remains correct.
- Confirm local progress is stored under `ModData/betterjournal/` and persists after restarting.


## After approval

1. Rebuild and run all checks against Vintage Story 1.22.7.
2. Verify `modinfo.json` and `BetterJournal.csproj` both say `0.1.1`, then check the ZIP/checksum.
3. Update release status in the README, changelog and validation record.
4. To publish on GitHub, use the draft release for tag `v0.1.1` and the notes in `docs/releases/v0.1.1.md`.
5. Use the package naming standard `BetterJournalv<version>.zip`. Attach `BetterJournalv0.1.1.zip` and `BetterJournalv0.1.1.zip.sha256`. GitHub supplies source archives for the tag.

The prepared ModDB fragment is `docs/moddb/description.html`, with a short summary and local preview beside it. It uses NiclAss's supplied page design. Its screenshot URLs use the public `NicIAss/niclass-images/betterjournal/` folder, separately from this code repository. The preview uses those same public image URLs.
