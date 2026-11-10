# Store listings

Everything the App Store and Google Play listings show for the phone app, kept in git and pushed to both stores by CI with fastlane (`deliver` for the App Store, `supply` for Google Play). Nobody signs in to App Store Connect or Play Console to change the listing, except for the items under [Set by hand](#set-by-hand).

## What lives where

| Path | What |
|---|---|
| `ios/metadata/en-US/*.txt` | App Store text: `name`, `subtitle`, `description`, `keywords`, `promotional_text`, `support_url`, `privacy_url`, `marketing_url`. No `release_notes.txt` yet: the first version shows none. |
| `ios/metadata/primary_category.txt`, `secondary_category.txt` | App Store categories as App Store Connect identifiers: `PRODUCTIVITY`, `DEVELOPER_TOOLS`. |
| `ios/metadata/review_information/notes.txt` | Notes for App Review. Only the notes: the contact and demo login are set by hand (the repo is public). |
| `ios/age_rating.json` | The age-rating answers, in App Store Connect's attribute names (fastlane's `app_rating_config_path` file). |
| `android/metadata/en-US/` | Play text: `title.txt`, `short_description.txt`, `full_description.txt`, and `changelogs/default.txt` (release notes for any version without its own `changelogs/<versionCode>.txt`). The listing push doesn't send them: release notes belong to a release, and it sends no binary. |
| `stage_images.py` | Stages the text and the images from `design/appstore/exports/` into fastlane's layout at run time, for each locale (below). |
| `push_listing.sh` | The only place fastlane is called: `push_listing.sh <ios\|android> <push\|verify>`. |
| `tests/` | `python3 -I -m unittest discover -s store/tests -v` |

### Locales

The listing is published in English (US) and English (Australia), `en-US` and `en-AU`, with the same text and images. The App Store app's primary language is English (Australia). The text lives once in git, under `en-US`; never add an `en-AU` folder here (`tests/test_stage_images.py` checks it, and staging refuses one). `stage_images.py` copies each staged `en-US` folder to `en-AU`, and fastlane reads both from the staged tree: `deliver` creates a missing version language by itself, and `supply` uploads every language folder. The locale list is `MIRROR_LOCALES` in `stage_images.py`.

Text files have no trailing newline or spaces: `supply` sends a file's bytes as they are, so a trailing newline counts against Play's limits (`deliver` strips them). `tests/test_layout.py` checks it.

The screenshots, the Play icon and the feature graphic stay in `design/appstore/exports/` (SHIP-930) and are not copied into `store/`. The screenshot captions and the notes on the stores' limits are in `design/appstore/listing.md`.

### Staging

`python3 -I store/stage_images.py <out> [--exports <dir>]` builds, in an empty or missing `<out>`:

- `<out>/ios/metadata/`: a copy of `ios/metadata/` (the categories, `review_information/` and `en-US/`). `deliver` reads the iOS text from here, not from `store/`.
- `<out>/ios/screenshots/en-US/`: the iPhone 6.9″ (1320 × 2868), 6.5″ (1284 × 2778) and 6.3″ (1206 × 2622) sets, named `iphone-<size>-<caption number>-<screen>.png` so each set uploads in caption order. `deliver` tells the sets apart by pixel size. The iPad 13″ set is left out while the app is iPhone-only (`supportsTablet: false`; SHIP-932).
- `<out>/android/metadata/`: a copy of `android/metadata/` plus `en-US/images/icon.png` (from `play/icon-512.png`), `featureGraphic.png`, and `phoneScreenshots/`, `sevenInchScreenshots/`, `tenInchScreenshots/` from `play/phone-screenshots`, `tablet-7-screenshots`, `tablet-10-screenshots`, in caption order.
- `<out>/ios/metadata/en-AU/`, `<out>/ios/screenshots/en-AU/` and `<out>/android/metadata/en-AU/` (text and images): byte-for-byte copies of the `en-US` folders beside them.

It exits 1, naming the path, when `ios/metadata/en-US/` or `android/metadata/en-US/` is missing, an `en-AU` folder is committed under `store/`, a source folder or file is missing, a screenshot folder doesn't hold exactly 8 PNGs, a screenshot folder holds anything else, a screenshot's name doesn't start with its caption number (`1-…` to `8-…`), or `<out>` isn't empty.

`check_listing.py --staged <out>` checks the images in every locale folder and that each `en-AU` folder is there with the same text as `en-US`.

## Changing the listing

1. Edit the text here, or re-export the images in `design/appstore/` (its README), in a pull request.
2. The **Store listing** check runs on every pull request that touches `store/` or `design/appstore/exports/`: it checks the stores' limits and the images, and runs a dry run against both stores. It isn't a required check (it's path-filtered), so read it before merging.
3. Merging changes nothing in the stores. The next release pushes it.

### What the dry run proves

- **Google Play:** Play validates the whole edit, text and images, then discards it. Nothing is published.
- **App Store:** `deliver` has no way to validate a listing without writing it, so the dry run only downloads the current listing into a temporary folder. That proves the API key, the app record and that `deliver` runs on the runner, **not** our text or screenshots. App Store problems show up at the release push.

## How a release pushes it

Run **Mobile release** (Actions) with `listing` on. After the build, it stages the listing and pushes it to the stores the run's `platform` names: on the App Store it replaces the screenshots and leaves the version unsubmitted; on Google Play it replaces the screenshots and images. A listing-only run (`listing` on, no build) pushes the listing alone.

Apple accepts changes to the name, subtitle, keywords, categories and screenshots only while a version is editable (Prepare for Submission), so the App Store push belongs with a release. Play takes listing changes at any time once a build has been uploaded; on a published app they go through review.

## Before the first push

1. The org secret `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (the Play service account key EAS already holds), shared with this repo like the `ASC_API_KEY_*` org secrets. It is not a repo secret. Without it the Android dry run is skipped with a notice.
2. Play: one build uploaded (an internal-track submit counts). Before that, Play refuses the dry run.
3. App Store: `version` in `src/ShipLean.Mobile/app.json` (`0.0.1` for the first release) matches the version on App Store Connect's Prepare for Submission page. The listing push writes to that version.
4. If the App Store dry run fails on Linux, set the repo variable `STORE_IOS_RUNNER` to `macos-latest`.

## Set by hand

The store APIs, or these tools, can't set these. They are one-off or rare.

| Store | Item | Why |
|---|---|---|
| Apple | App Privacy ("nutrition label") | `deliver` can't set it. fastlane's separate `upload_app_privacy_details_to_app_store` exists but wasn't checked (it may need an Apple ID login, not the API key). |
| Apple | Pricing and availability, EU trader status | Set once in App Store Connect. |
| Apple | App Review contact (name, email, phone) | The repo is public. `deliver` leaves fields it isn't given alone. |
| Apple | App Review demo login | Not in git (public repo); the demo account doesn't exist yet (SHIP-930 D4). |
| Apple | Copyright | Not in git yet: whose name it carries is Joel's call. A `ios/metadata/copyright.txt` would push it from git. |
| Play | Data safety form | No Play API. |
| Play | Content rating (IARC questionnaire) | No Play API. |
| Play | Target audience, ads, app access (reviewer login), account-deletion URL, category (Productivity) | Play Console declarations, no API. |

### Age rating

`ios/age_rating.json` holds the lowest answer for every question (`NONE`, or `false`), confirmed by Joel on 11 Nov 2026: the app has no adult content. The release push sends this file as it is.
