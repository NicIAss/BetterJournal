# First release preparation

Prepared version: **v0.1.1**, mod ID `betterjournal`, author **NiclAss**, target game **1.22.7**.

The repository is private. The prepared build is identified by tag `v0.1.1`; publishing the GitHub release remains a manual step. There is no automatic publishing workflow.

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
4. Make the repository public when approved and publish a GitHub release from the prepared `v0.1.1` tag using `docs/releases/v0.1.1.md`.
5. Use the package naming standard `BetterJournalv<version>.zip`. Attach `BetterJournalv0.1.1.zip` and `BetterJournalv0.1.1.zip.sha256`. GitHub supplies source archives for the tag.

The prepared ModDB fragment is `docs/moddb/description.html`, with a short summary and local preview beside it. It uses NiclAss's supplied page design. Its screenshot URLs use the public `NicIAss/niclass-images/betterjournal/` folder, so the images work while the BetterJournal code repository is private. The preview uses those same public image URLs.
