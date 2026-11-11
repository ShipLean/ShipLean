# Releasing the phone app

How the phone app (`src/ShipLean.Mobile`) gets from `main` to testers and the stores, written as the things people actually come here to do. Every step is a GitHub Actions workflow you start by hand, and every command below can be pasted as it is (run them from anywhere; `-R` names the repo).

You need the [GitHub CLI](https://cli.github.com/) signed in with access to `ShipLean/ShipLean`. Nothing here needs an Expo, App Store Connect or Play Console login: the workflows hold the keys.

## The pieces

| Workflow | File | What it does |
|---|---|---|
| **Mobile release** | `.github/workflows/mobile-release.yml` | Builds on EAS, sends the build to TestFlight and to Play's internal testers, saves the `.aab`/`.ipa` on the run, and pushes the store listing from `store/`. |
| **Store submit** | `.github/workflows/store-submit.yml` | Sends the newest build to review: Play closed testing (`alpha`) or production, and App Store review. `main` only. |
| **Store listing** | `.github/workflows/store-listing.yml` | The pull request check for `store/`: limits, images, and a dry run against both stores. Pushes nothing. |

The listing text, images and the scripts behind all three live in [`store/`](../store/README.md).

The version is `major.minor.patch`: major and minor are `expo.version` in `src/ShipLean.Mobile/app.json`, and the patch is the Mobile release run number, so a release needs no version commit ([Version](../store/README.md#version)).

## Watching a run

Each journey starts a workflow and then watches it. These two lines are the same everywhere; swap the workflow file name.

```bash
# Follow the newest run of a workflow until it ends (exit code is the run's result)
sleep 10; gh run watch -R ShipLean/ShipLean --exit-status $(gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 1 --json databaseId --jq '.[0].databaseId')

# Or list the last few runs and open one in the browser
gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 5
gh run view -R ShipLean/ShipLean --web <run-id>
```

The `sleep` gives GitHub a moment to register a run you just started, so the list returns it and not the one before.

## Put a new build in testers' hands

The usual journey after merging app changes. Builds both platforms, then iOS lands in TestFlight and Android is rolled out to Play's internal testers, with the listing pushed alongside.

```bash
gh workflow run mobile-release.yml -R ShipLean/ShipLean --ref main
sleep 10; gh run watch -R ShipLean/ShipLean --exit-status $(gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 1 --json databaseId --jq '.[0].databaseId')
```

One platform only (saves an EAS build credit):

```bash
gh workflow run mobile-release.yml -R ShipLean/ShipLean --ref main -f platform=android
gh workflow run mobile-release.yml -R ShipLean/ShipLean --ref main -f platform=ios
```

Build without sending it to the stores (to try the files first):

```bash
gh workflow run mobile-release.yml -R ShipLean/ShipLean --ref main -f submit=false -f listing=false
```

- It takes as long as EAS does, usually 15–40 minutes. Follow the build itself on [expo.dev](https://expo.dev) (account `dickos-team`).
- Android testers get it as soon as Play processes it. iOS internal testers see it in TestFlight once Apple has processed it (5–30 minutes); external TestFlight testers need Apple's beta review, started in App Store Connect.
- Every build run uses EAS build credits and takes the next version number.

## Grab the built files

Every build run saves what it built as the `app-builds` artifact, kept 90 days: `shiplean-android-<version>-<versionCode>.aab` and `shiplean-ios-<version>-<buildNumber>.ipa`.

```bash
# From the newest Mobile release run, into ./app-builds
gh run download -R ShipLean/ShipLean -n app-builds -D app-builds $(gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 1 --json databaseId --jq '.[0].databaseId')

# From a particular run
gh run download -R ShipLean/ShipLean -n app-builds -D app-builds <run-id>
```

An `.aab` is what Play takes; it doesn't install on a phone directly (`bundletool` can turn it into installable APKs). An `.ipa` is signed for the App Store and installs only through TestFlight.

## Change the store listing

Text, keywords, screenshots, the icon or the feature graphic.

1. Edit `store/` in a pull request: the text is in `store/ios/metadata/en-US/` and `store/android/metadata/en-US/`, the images are drawn in `store/assets/` and re-exported with `store/assets/export.sh` ([`store/README.md`](../store/README.md), [`store/assets/README.md`](../store/assets/README.md)).
2. The **Store listing** check runs on the pull request. It isn't required, so read it before merging.
3. After merging, push the listing without building:

```bash
gh workflow run mobile-release.yml -R ShipLean/ShipLean --ref main -f build=false -f listing=true
sleep 10; gh run watch -R ShipLean/ShipLean --exit-status $(gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 1 --json databaseId --jq '.[0].databaseId')
```

Apple only takes the name, subtitle, keywords and screenshots while a version is on Prepare for Submission; a listing-only run writes to that version and doesn't rename it. On a published Play app, listing changes go through Google's review.

## Start or update the Play closed test

A new personal Play developer account must run a closed test with at least 12 testers for 14 days before Play allows production. This puts the build internal testers have on **Closed testing – Alpha** and sends it to Google's review.

```bash
gh workflow run store-submit.yml -R ShipLean/ShipLean --ref main -f platform=android -f play_track=alpha
sleep 10; gh run watch -R ShipLean/ShipLean --exit-status $(gh run list -R ShipLean/ShipLean --workflow store-submit.yml -L 1 --json databaseId --jq '.[0].databaseId')
```

- It promotes the **rolled-out internal release**, so [put the build in testers' hands](#put-a-new-build-in-testers-hands) first and let that run finish.
- **The first time:** until its first review Play calls the app a *draft app* and takes only draft releases outside internal testing. Add `-f play_release_status=draft`, then send that release for review in Play Console › Publishing overview. Every later run can leave it at the default, `completed`.

```bash
gh workflow run store-submit.yml -R ShipLean/ShipLean --ref main -f platform=android -f play_track=alpha -f play_release_status=draft
```
- The tester list (email list or Google Group) and countries are set once in Play Console under Closed testing – Alpha › Testers.
- Each Google account counts once: `name+tag@gmail.com` addresses are the same account.

## Go live

When the build testers have is the one to ship.

**Google Play** (after the closed test, if your account needs one):

```bash
gh workflow run store-submit.yml -R ShipLean/ShipLean --ref main -f platform=android -f play_track=production
```

Google reviews it and publishes it when approved, unless Managed publishing is on in Play Console.

**App Store:**

```bash
gh workflow run store-submit.yml -R ShipLean/ShipLean --ref main -f platform=ios
```

It attaches the newest TestFlight build to the version on Prepare for Submission and submits it for App Review. The build's version must match that version, which a Mobile release run that builds with `listing` on takes care of. After approval the version waits: release it in App Store Connect (App Store › the version › **Release this version**).

**Both at once** (Play to production):

```bash
gh workflow run store-submit.yml -R ShipLean/ShipLean --ref main -f platform=all -f play_track=production
sleep 10; gh run watch -R ShipLean/ShipLean --exit-status $(gh run list -R ShipLean/ShipLean --workflow store-submit.yml -L 1 --json databaseId --jq '.[0].databaseId')
```

A submission can't be taken back from CI. To pull one, use the store's console (App Store Connect: Remove from review; Play Console: the release's page).

## Bump the major or minor version

For a release that deserves `1.0` or `0.2`: change the first two numbers of `expo.version` in `src/ShipLean.Mobile/app.json` in a pull request and leave the patch at `0`. The next build run takes the run number as the patch, for example `0.2.17`. Never lower them: the stores refuse a version below one they already have.

## When a run fails

```bash
# The failed steps' logs of the newest run
gh run view -R ShipLean/ShipLean --log-failed $(gh run list -R ShipLean/ShipLean --workflow mobile-release.yml -L 1 --json databaseId --jq '.[0].databaseId')

# Re-run only the failed jobs (a build run re-runs the EAS build too)
gh run rerun -R ShipLean/ShipLean --failed <run-id>
```

| Message | What it means |
|---|---|
| `Add the … repository secret first` | A secret is missing. They are org secrets (`EXPO_TOKEN`, `ASC_API_KEY_*`, `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`) shared with this repo. |
| `Google Api Error: … Package not found` | Play can't see the app: the service account isn't invited to it in Play Console, or no build was ever uploaded by hand. |
| `Only releases with status draft may be created on draft app` | Play still calls the app a draft app (never reviewed). For Store submit, run it with `-f play_release_status=draft` and send the release for review in Play Console › Publishing overview. If Mobile release's internal upload says it, set `releaseStatus` to `draft` in `src/ShipLean.Mobile/eas.json` until the first review. |
| `Track 'internal' doesn't have any releases` | Store submit found nothing rolled out to internal testers: run a build first. |
| Play or Apple names a missing declaration | Something only a person can set in the console is missing (App Privacy, Data safety, content rating, target audience, the reviewer login). The list is in [Set by hand](../store/README.md#set-by-hand). |
| `eas build exited with …` | A build or submission failed on EAS. The Build step's log and expo.dev say why; the files that did build are still saved on the run. |

## Set by hand, once

Some things have no API and are done once in the consoles: App Privacy, pricing, the App Review contact and demo login, copyright, Data safety, the content rating, target audience, and Play's closed-test testers. The full list and why: [`store/README.md` › Set by hand](../store/README.md#set-by-hand).
