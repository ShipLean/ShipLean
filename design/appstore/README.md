# App Store and Google Play assets (SHIP-930)

Icons, splash images, the Play feature graphic, store screenshots and listing text for the phone app (epic SHIP-905). Everything is drawn from `../logo` (the boat glyph, copied unchanged) and the mock-ups in `../mobileapp`.

| File | What it is |
|---|---|
| `assets.js`, `asset.html` | Every asset in one list. `asset.html?id=<id>` draws one at its exact pixel size, and `asset.html` with no id lists them. |
| `export.sh` | Writes `exports/` with headless Chrome, then runs `to-rgba.py` (the alpha assets must be 32-bit) and `check-png.py` (exact size and alpha). `./export.sh play-` exports only the ids starting with `play-`. |
| `listing.md` | Store text, screenshot captions and Joel's decisions D1–D10. |
| `exports/ios`, `exports/android`, `exports/expo`, `exports/play` | Icons, splash images and the feature graphic. The screenshot folders are git-ignored: they're drafts until the app is built (D4), and the full set is attached to SHIP-907. |

On the shared host: `CHROME=$(ls ~/.cache/ms-playwright/chromium_headless_shell-*/*/chrome-headless-shell) LD_LIBRARY_PATH=~/.local/pwlibs/usr/lib/x86_64-linux-gnu ./export.sh`.
