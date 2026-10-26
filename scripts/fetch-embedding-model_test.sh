#!/usr/bin/env bash
# Tests for fetch-embedding-model.sh. No network: the manifests point at file:// copies of small fake files.
# Usage: scripts/fetch-embedding-model_test.sh
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
fetch="$here/fetch-embedding-model.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failed=0

# A source folder with two fake files and a manifest naming them by file:// url.
make_manifest() { # <name> [sha-of-b-override]
  local src="$work/$1/src"
  mkdir -p "$src"
  printf 'model bytes' >"$src/a.bin"
  printf 'tokenizer bytes' >"$src/b.bin"
  local sha_a sha_b
  sha_a="$(sha256sum "$src/a.bin" | cut -d' ' -f1)"
  sha_b="${2:-$(sha256sum "$src/b.bin" | cut -d' ' -f1)}"
  printf '# fake\nmodel.onnx file://%s/a.bin %s\n\nsentencepiece.bpe.model file://%s/b.bin %s\n' \
    "$src" "$sha_a" "$src" "$sha_b" >"$work/$1/manifest"
}

check() { # <test name> <condition exit code> <message>
  if [ "$2" -eq 0 ]; then echo "PASS $1"; else echo "FAIL $1: $3"; failed=1; fi
}

fetch_WhenRunTwice_ShouldReuseTheCache() {
  make_manifest twice
  local out="$work/twice/out/model"
  "$fetch" "$out" "$work/twice/manifest" >/dev/null || return 1
  # With the sources gone a second download would fail, so success means the first copy was reused.
  rm -r "$work/twice/src"
  local second
  second="$("$fetch" "$out" "$work/twice/manifest")" || return 1
  case "$second" in *"already in"*) ;; *) echo "second run did not say it reused: $second"; return 1 ;; esac
  [ "$(cat "$out/model.onnx")" = "model bytes" ]
}

fetch_WhenShaWrong_ShouldFailAndLeaveNoDirectory() {
  make_manifest wrong 0000000000000000000000000000000000000000000000000000000000000000
  local out="$work/wrong/out/model"
  if "$fetch" "$out" "$work/wrong/manifest" 2>/dev/null; then echo "fetch succeeded with a wrong sha"; return 1; fi
  [ ! -e "$out" ] || { echo "directory left behind"; return 1; }
  local leftovers
  leftovers="$(find "$work/wrong/out" -mindepth 1 -type d)"
  [ -z "$leftovers" ] || { echo "temp directories left behind: $leftovers"; return 1; }
}

fetch_WhenRunConcurrently_ShouldProduceOneDirectory() {
  make_manifest parallel
  local out="$work/parallel/out/model" pids=() i
  for i in 1 2 3 4 5; do
    "$fetch" "$out" "$work/parallel/manifest" >/dev/null 2>&1 &
    pids+=($!)
  done
  for i in "${pids[@]}"; do wait "$i" || { echo "a concurrent run failed"; return 1; }; done
  local dirs
  dirs="$(find "$work/parallel/out" -mindepth 1 -type d)"
  [ "$dirs" = "$out" ] || { echo "expected only $out, found: $dirs"; return 1; }
  [ "$(cat "$out/sentencepiece.bpe.model")" = "tokenizer bytes" ]
}

fetch_WhenManifestShaMalformed_ShouldFailBeforeDownloading() {
  make_manifest malformed not-a-sha
  local out="$work/malformed/out/model"
  if "$fetch" "$out" "$work/malformed/manifest" 2>/dev/null; then echo "fetch accepted a malformed sha"; return 1; fi
  [ ! -e "$out" ]
}

fetch_WhenKilledRunLeftTempFolders_ShouldRemoveThem() {
  make_manifest leftovers
  local out="$work/leftovers/out/model"
  mkdir -p "$out.tmp.dead" "$out.tmp.dead.old"
  "$fetch" "$out" "$work/leftovers/manifest" >/dev/null || return 1
  [ ! -e "$out.tmp.dead" ] && [ ! -e "$out.tmp.dead.old" ]
}

for t in fetch_WhenManifestShaMalformed_ShouldFailBeforeDownloading fetch_WhenKilledRunLeftTempFolders_ShouldRemoveThem \
  fetch_WhenRunTwice_ShouldReuseTheCache fetch_WhenShaWrong_ShouldFailAndLeaveNoDirectory \
  fetch_WhenRunConcurrently_ShouldProduceOneDirectory; do
  "$t"
  check "$t" $? "see output above"
done
exit $failed
