# ModDB page

- `summary.txt`: short summary for the ModDB summary field.
- `description.html`: HTML fragment to paste into the description's source editor.
- `preview.html`: preview with public screenshot URLs and collapsible sections. Its wrapper/script are preview helpers, not part of the paste fragment.

The page reuses the supplied BetterTraders page's cream background, buttons, spoiler sections and design credit. It uses plain feature sentences and the BetterJournal repository/issue URLs. The Discord invite is retained from the supplied template. There is no BetterTraders team banner, world-generation copy or hidden trader-keyword block.

Screenshots in both HTML files use the public `NicIAss/niclass-images` repository under `betterjournal/`: `bookshelf.png`, `book.png`, `tapestries.png` and `tapestrysingleview.png`. They work independently of the private BetterJournal code repository. The supplied GitHub blob links are used in their raw-image form so browsers display the PNG files. The ModDB page has not been published.

When editing the description, keep the preview's body synchronized with the fragment. Both use the same public screenshot URLs. Progress behavior is documented in [PROGRESS.md](../PROGRESS.md).
