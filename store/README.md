# Store listings

Everything the App Store and Google Play listings show for the phone app, kept in git and pushed to both stores by CI with fastlane (`deliver` for the App Store, `supply` for Google Play). Nobody signs in to App Store Connect or Play Console to change the listing, except for the items under [Set by hand](#set-by-hand).

## What lives where

| Path | What |
|---|---|
| `ios/metadata/en-US/*.txt` | App Store text: `name`, `subtitle`, `description`, `keywords`, `promotional_text`, `support_url`, `privacy_url`, `marketing_url`. No `release_notes.txt` yet: the first version shows none. |
| `ios/metadata/primary_category.txt`, `secondary_category.txt` | App Store categories as App Store Connect identifiers: `PRODUCTIVITY`, `DEVELOPER_TOOLS`. |
| `ios/metadata/review_information/notes.txt` | Notes for App Review. Only the notes: the contact and demo login are set by hand (the repo is public). |
| `ios/age_rating.json` | The age-rating answers, in App Store Connect's attribute names (fastlane's `app_rating_config_path` file). |
| `android/metadata/en-US/` | Play text: `title.txt`, `short_description.txt`, `full_description.txt`, and `changelogs/default.txt` (release notes for any version without its own `changelogs/<versionCode>.txt`). |
| `stage_images.py` | Copies the images from `design/appstore/exports/` into fastlane's layout at run time (below). |
| `push_listing.sh` | The only place fastlane is called: `push_listing.sh <ios\|android> <push\|verify>`. |
| `tests/` | `python3 -I -m unittest discover -s store/tests -v` |

Text files have no trailing newline or spaces: `supply` sends a file's bytes as they are, so a trailing newline counts against Play's limits (`deliver` strips them). `tests/test_layout.py` checks it.

The screenshots, the Play icon and the feature graphic stay in `design/appstore/exports/` (SHIP-930) and are not copied into `store/`. The screenshot captions and the notes on the stores' limits are in `design/appstore/listing.md`.

### Images

`python3 -I store/stage_images.py <out> [--exports <dir>]` builds, in an empty or missing `<out>`:

- `<out>/ios/screenshots/en-US/`: the iPhone 6.9″ (1320 × 2868), 6.5″ (1284 × 2778) and 6.3″ (1206 × 2622) sets, named `iphone-<size>-<caption number>-<screen>.png` so each set uploads in caption order. `deliver` tells the sets apart by pixel size. The iPad 13″ set is left out while the app is iPhone-only (`supportsTablet: false`; SHIP-932).
- `<out>/android/metadata/`: a copy of `android/metadata/` plus `en-US/images/icon.png` (from `play/icon-512.png`), `featureGraphic.png`, and `phoneScreenshots/`, `sevenInchScreenshots/`, `tenInchScreenshots/` from `play/phone-screenshots`, `tablet-7-screenshots`, `tablet-10-screenshots`, in caption order.

It exits 1, naming the path, when a source folder or file is missing, a screenshot folder doesn't hold exactly 8 PNGs, a screenshot folder holds anything else, a screenshot's name doesn't start with its caption number (`1-…` to `8-…`), or `<out>` isn't empty.

## Changing the listing

1. Edit the text here, or re-export the images in `design/appstore/` (its README), in a pull request.
2. The **Store listing** check runs on every pull request that touches `store/` or `design/appstore/exports/`: it checks the stores' limits and the images, and runs a dry run against both stores. It isn't a required check (it's path-filtered), so read it before merging.
3. Merging changes nothing in the stores. The next release pushes it.

### What the dry run proves

- **Google Play:** Play validates the whole edit, text and images, then discards it. Nothing is published.
- **App Store:** `deliver` has no way to validate a listing without writing it, so the dry run only downloads the current listing into a temporary folder. That proves the API key, the app record and that `deliver` runs on the runner, **not** our text or screenshots. App Store problems show up at the release push.

## How a release pushes it

Run **Mobile release** (Actions) with `listing` on. After the build, it stages the images and pushes the listing to the stores the run's `platform` names: on the App Store it replaces the screenshots and leaves the version unsubmitted; on Google Play it replaces the screenshots and images. A listing-only run (`listing` on, no build) pushes the listing alone.

Apple accepts changes to the name, subtitle, keywords, categories and screenshots only while a version is editable (Prepare for Submission), so the App Store push belongs with a release. Play takes listing changes at any time once a build has been uploaded; on a published app they go through review.

## Before the first push

1. The repo secret `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` (the Play service account key EAS already holds). Without it the Android dry run is skipped with a notice.
2. Play: one build uploaded (an internal-track submit counts). Before that, Play refuses the dry run.
3. App Store: `version` in `src/ShipLean.Mobile/app.json` set to `1.0.0`, and a build in App Store Connect, so an editable version exists.
4. If the App Store dry run fails on Linux, set the repo variable `STORE_IOS_RUNNER` to `macos-latest`.
5. Joel confirms the age-rating answers (below).

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

TODO: Joel confirms `ios/age_rating.json` before the first real push; until then it holds the lowest answer for every question (`NONE`, or `false`). Check in particular `userGeneratedContent` and `messagingAndChat`: people write notes and comments their team reads, which may need `true`. The release push sends this file as it is.
