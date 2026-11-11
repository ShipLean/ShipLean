#!/usr/bin/env bash
# Push or verify a store listing, or submit the newest build for review, with fastlane (contract
# C5). This is the only place fastlane is called, and the only place its flags live. Run from the
# repo root:
#
#   store/push_listing.sh <ios|android> <push|verify|submit>
#
# Environment:
#   ASC_KEY_PATH   App Store Connect API key JSON (ios), from store/write_keys.py
#   PLAY_KEY_PATH  Play service account JSON (android), from store/write_keys.py
#   STAGED         the staged listing from store/stage_images.py: text and images, en-US and
#                  its mirrored locales (ios push, android push and verify)
#   PLAY_TRACK     android submit: the track to promote the internal release to, alpha (closed
#                  testing) or production
#   PLAY_RELEASE_STATUS  android submit: completed (sent to Google's review) or draft (for an app
#                  Play still calls a draft app: send it for review in Play Console)
#   APP_VERSION_PATCH  optional (ios push): set by a run that also builds; the push then targets
#                  that build's version, from store/app_version.py
#
# Exit 2 on bad arguments, 1 when a variable it needs is missing, otherwise fastlane's exit code.
set -euo pipefail

usage() {
  echo "usage: store/push_listing.sh <ios|android> <push|verify|submit>" >&2
  exit 2
}

# The named variable must hold the path of an existing key file.
require_key_file() {
  local name=$1
  local value=${!name:-}
  if [ -z "$value" ] || [ ! -f "$value" ]; then
    echo "push_listing.sh: $name must be the path of the key file (from store/write_keys.py)" >&2
    exit 1
  fi
}

require_staged() {
  if [ -z "${STAGED:-}" ] || [ ! -d "$STAGED" ]; then
    echo "push_listing.sh: STAGED must be the staged images folder (from store/stage_images.py)" >&2
    exit 1
  fi
}

[ "$#" -eq 2 ] || usage
platform=$1
mode=$2
case "$platform" in ios | android) ;; *) usage ;; esac
case "$mode" in push | verify | submit) ;; *) usage ;; esac

export BUNDLE_GEMFILE=store/Gemfile
app_id=app.shiplean.mobile

# submit: send the newest build for store review (the Store submit workflow). The listing is
# not pushed again here; Mobile release pushes it.
if [ "$mode" = submit ]; then
  if [ "$platform" = ios ]; then
    require_key_file ASC_KEY_PATH
    # submit_build attaches the newest processed build ("latest", waiting while it processes) to
    # the editable App Store version and submits it. The skips keep it from touching the listing.
    # Export compliance needs no answer here: app.json sets ITSAppUsesNonExemptEncryption false.
    bundle exec fastlane deliver submit_build \
      --api_key_path "$ASC_KEY_PATH" \
      --app_identifier "$app_id" \
      --build_number latest \
      --skip_metadata true \
      --skip_screenshots true \
      --skip_binary_upload true \
      --force true \
      --run_precheck_before_submit false
  else
    require_key_file PLAY_KEY_PATH
    case "${PLAY_TRACK:-}" in
      alpha | production) ;;
      *)
        echo "push_listing.sh: PLAY_TRACK must be alpha (closed testing) or production, found '${PLAY_TRACK:-}'" >&2
        exit 1
        ;;
    esac
    # Until its first review Play calls the app a draft app and refuses anything but a draft
    # release outside internal testing ("Only releases with status draft may be created on draft
    # app"); the first send for review is then done once in Play Console.
    case "${PLAY_RELEASE_STATUS:-}" in
      completed | draft) ;;
      *)
        echo "push_listing.sh: PLAY_RELEASE_STATUS must be completed or draft, found '${PLAY_RELEASE_STATUS:-}'" >&2
        exit 1
        ;;
    esac
    # Promotes the internal track's rolled-out release (what internal testers have) to PLAY_TRACK.
    # completed sends it to Google's review; draft leaves it on the track for Play Console. No
    # binary, listing or release notes.
    bundle exec fastlane supply \
      --json_key "$PLAY_KEY_PATH" \
      --package_name "$app_id" \
      --track internal \
      --track_promote_to "$PLAY_TRACK" \
      --track_promote_release_status "$PLAY_RELEASE_STATUS" \
      --skip_upload_aab true \
      --skip_upload_apk true \
      --skip_upload_metadata true \
      --skip_upload_changelogs true \
      --skip_upload_images true \
      --skip_upload_screenshots true
  fi
  exit 0
fi

if [ "$platform" = ios ]; then
  require_key_file ASC_KEY_PATH
  if [ "$mode" = push ]; then
    require_staged
    # With APP_VERSION_PATCH (a run that also builds), the listing goes to that build's version,
    # which deliver creates or renames the editable version to. Without it (a listing-only run),
    # --app_version is left out and deliver updates the version that is editable now.
    version_args=()
    if [ -n "${APP_VERSION_PATCH:-}" ]; then
      version_args=(--app_version "$(python3 -I store/app_version.py)")
    fi
    repo=$PWD
    # deliver 2.241.2 asks "No deliver configuration found ... setup deliver?" unless the current
    # directory has a Deliverfile or a metadata/ folder (--metadata_path doesn't count), and a
    # non-interactive run crashes on the question. The staged ios/ folder has metadata/, so run
    # from there with every other path absolute.
    cd "$STAGED/ios"
    BUNDLE_GEMFILE="$repo/store/Gemfile" bundle exec fastlane deliver \
      --api_key_path "$ASC_KEY_PATH" \
      --app_identifier "$app_id" \
      "${version_args[@]}" \
      --metadata_path "$STAGED/ios/metadata" \
      --screenshots_path "$STAGED/ios/screenshots" \
      --app_rating_config_path "$repo/store/ios/age_rating.json" \
      --skip_binary_upload true \
      --overwrite_screenshots true \
      --submit_for_review false \
      --automatic_release false \
      --force true \
      --run_precheck_before_submit false
  else
    # deliver has no no-write listing validation, so the iOS dry run downloads the live metadata
    # into a throwaway folder. That proves the key, the app record and that deliver runs on this
    # runner. It does NOT check our text or screenshots: App Store problems surface at the push.
    # --force true is needed: without it a non-interactive run skips the download and exits 0.
    bundle exec fastlane deliver download_metadata \
      --api_key_path "$ASC_KEY_PATH" \
      --app_identifier "$app_id" \
      --metadata_path "$(mktemp -d)" \
      --force true
  fi
else
  require_key_file PLAY_KEY_PATH
  require_staged
  # supply ties a listing upload to one release of --track, for release notes, even with them
  # skipped: with no --version_code the track must hold exactly one release, and it fails with
  # "More than one release found in this track" once internal has a draft beside the rolled-out
  # release. Name the newest build on internal, which is in exactly one of its releases.
  lookup=$(FASTLANE_DISABLE_COLORS=1 bundle exec fastlane run google_play_track_version_codes \
    json_key:"$PLAY_KEY_PATH" package_name:"$app_id" track:internal)
  version_code=$(printf '%s\n' "$lookup" | sed -n 's/.*Result: \[\(.*\)\].*/\1/p' | tr -c '0-9' '\n' \
    | sed '/^$/d' | sort -n | tail -n 1)
  if [ -z "$version_code" ]; then
    echo "push_listing.sh: Play's internal track has no build; upload one first (Mobile release)" >&2
    exit 1
  fi
  args=(
    --json_key "$PLAY_KEY_PATH"
    --package_name "$app_id"
    --metadata_path "$STAGED/android/metadata"
    --skip_upload_aab true
    --skip_upload_apk true
    # Release notes (changelogs/) belong to a release's version code, and this push sends no
    # binary. Without the skip, supply 2.241.2 fails "Cannot find changelog because no version
    # code given". EAS submit ships the binary.
    --skip_upload_changelogs true
    --track internal
    --version_code "$version_code"
  )
  if [ "$mode" = verify ]; then
    # Play validates the whole edit (text and images) and then discards it.
    args+=(--validate_only true)
  fi
  bundle exec fastlane supply "${args[@]}"
fi
