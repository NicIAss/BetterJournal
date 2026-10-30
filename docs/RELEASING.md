# First release preparation

Prepared version: **v0.1.0**, mod ID `betterjournal`, author **NiclAss**, target game **1.22.7**.

The repository is private while final testing is pending. There is no automatic publishing workflow. A GitHub tag/release must wait for NiclAss's approval after testing.

## Final in-game pass

- Confirm the installed mod list shows BetterJournal 0.1.0, NiclAss and the selected screenshot icon.
- Open/close J, switch Books/Tapestries and return with Back/Escape.
- Open a vanilla and a modded book; check buttons, swiping, animations and A-/A+.
- Read the final available text; confirm NEW clears, the bookmark behaves correctly and reopening resumes at unread content/the saved position.
- Check Nightfall, Rot, Rotbeast, Schematic D, Blackguard Map and Forlorn Hope for correct artwork, names and a single correct lore passage.
- Verify partial/rotten tapestry pieces and completed NEW prompts.
- Leave/rejoin the multiplayer server and confirm progress remains correct.
- Confirm local progress is stored under `ModData/betterjournal/` and persists after restarting.


## After approval

1. Rebuild and run all checks against Vintage Story 1.22.7.
2. Verify `modinfo.json` and `BetterJournal.csproj` both say `0.1.0`, then check the ZIP/checksum.
3. Update release status in the README, changelog and validation record.
4. Make the repository public when approved, create tag `v0.1.0` at the tested commit, and publish a GitHub release using `docs/releases/v0.1.0.md`.
5. Attach `betterjournal-0.1.0.zip` and `betterjournal-0.1.0.zip.sha256`. GitHub supplies source archives for the tag.

The prepared ModDB fragment is `docs/moddb/description.html`, with a short summary and local preview beside it. It uses NiclAss's supplied page design. Its public screenshot URLs reference `docs/images/` in this repository; make the repository public before publishing the ModDB description so those URLs resolve for players. The preview uses local images and works before publication.
