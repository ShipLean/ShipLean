#!/usr/bin/env bash
# Push or verify a store listing with fastlane (contract C5). This is the only place fastlane
# is called, and the only place its flags live. Run from the repo root:
#
#   store/push_listing.sh <ios|android> <push|verify>
#
# Environment:
#   ASC_KEY_PATH   App Store Connect API key JSON (ios), from store/write_keys.py
#   PLAY_KEY_PATH  Play service account JSON (android), from store/write_keys.py
#   STAGED         the staged listing from store/stage_images.py: text and images, en-US and
#                  its mirrored locales (ios push, android push and verify)
#
# Exit 2 on bad arguments, 1 when a variable it needs is missing, otherwise fastlane's exit code.
set -euo pipefail

usage() {
  echo "usage: store/push_listing.sh <ios|android> <push|verify>" >&2
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
case "$mode" in push | verify) ;; *) usage ;; esac

export BUNDLE_GEMFILE=store/Gemfile
app_id=app.shiplean.mobile

if [ "$platform" = ios ]; then
  require_key_file ASC_KEY_PATH
  if [ "$mode" = push ]; then
    require_staged
    version=$(python3 -I -c 'import json, sys; print(json.load(open(sys.argv[1]))["expo"]["version"])' \
      src/ShipLean.Mobile/app.json)
    bundle exec fastlane deliver \
      --api_key_path "$ASC_KEY_PATH" \
      --app_identifier "$app_id" \
      --app_version "$version" \
      --metadata_path "$STAGED/ios/metadata" \
      --screenshots_path "$STAGED/ios/screenshots" \
      --app_rating_config_path store/ios/age_rating.json \
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
  )
  if [ "$mode" = verify ]; then
    # Play validates the whole edit (text and images) and then discards it.
    args+=(--validate_only true)
  fi
  bundle exec fastlane supply "${args[@]}"
fi
