#!/usr/bin/env bash
# Tests fetch-models.sh against local file:// URLs: no network.
# Run from src/ShipLean.Mobile: bash tools/voice-eval/fetch-models.test.sh
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
fetch="$here/fetch-models.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

failures=0
pass() { echo "ok - $1"; }
fail() { echo "not ok - $1" >&2; failures=$((failures + 1)); }
sha() { sha256sum "$1" | cut -d' ' -f1; }

src="$work/src"
mkdir -p "$src/tool"
head -c 300000 /dev/urandom >"$src/encoder.int8.onnx"
printf 'a 0\nb 1\n' >"$src/tokens.txt"
head -c 200000 /dev/urandom >"$src/model.gguf"
echo 'llama' >"$src/tool/llama-cli"
tar -czf "$src/tool.tar.gz" -C "$src" tool

# manifest.json in the exporter's shape (MODEL_FILES), with a SHA-256 that may be overridden for the model file.
models_json() { # <out> [sha for model.gguf]
  local gguf_sha="${2:-$(sha "$src/model.gguf")}"
  cat >"$1" <<JSON
[
  { "id": "parakeet-encoder", "model": "parakeet", "fileName": "encoder.int8.onnx",
    "url": "file://$src/encoder.int8.onnx", "sha256": "$(sha "$src/encoder.int8.onnx")" },
  { "id": "parakeet-tokens", "model": "parakeet", "fileName": "tokens.txt",
    "url": "file://$src/tokens.txt", "sha256": "$(sha "$src/tokens.txt")" },
  { "id": "qwen3-4b-q4_k_m", "model": "qwen3-4b", "fileName": "Qwen3-4B-Q4_K_M.gguf",
    "url": "file://$src/model.gguf", "sha256": "$gguf_sha" }
]
JSON
}
models_json "$work/manifest.json"
printf '# tools\n\nllama.cpp/tool.tar.gz file://%s %s\n' "$src/tool.tar.gz" "$(sha "$src/tool.tar.gz")" >"$work/eval.manifest"

# 1. Every file lands at <cache>/<model>/<fileName>, checked, and an archive is unpacked.
cache="$work/cache"
if bash "$fetch" "$cache" "$work/manifest.json" "$work/eval.manifest" >"$work/out1" 2>&1 &&
  cmp -s "$cache/parakeet/encoder.int8.onnx" "$src/encoder.int8.onnx" &&
  cmp -s "$cache/parakeet/tokens.txt" "$src/tokens.txt" &&
  cmp -s "$cache/qwen3-4b/Qwen3-4B-Q4_K_M.gguf" "$src/model.gguf" &&
  [ -f "$cache/llama.cpp/tool/tool/llama-cli" ] &&
  [ -z "$(find "$cache" -name '*.part')" ]; then
  pass 'fetches every file into <cache>/<model>/<fileName> and unpacks an archive'
else
  fail 'fetches every file into <cache>/<model>/<fileName> and unpacks an archive'; cat "$work/out1" >&2
fi

# 2. A wrong SHA-256 is rejected and nothing is renamed into place.
bad="$work/bad"
models_json "$work/bad.json" "$(printf '0%.0s' {1..64})"
if bash "$fetch" "$bad" "$work/bad.json" "$work/eval.manifest" >"$work/out2" 2>&1; then
  fail 'a wrong SHA-256 is rejected and nothing is renamed into place (it succeeded)'
elif [ ! -e "$bad/qwen3-4b/Qwen3-4B-Q4_K_M.gguf" ] && [ ! -e "$bad/qwen3-4b/Qwen3-4B-Q4_K_M.gguf.part" ] &&
  grep -q 'SHA-256 mismatch for qwen3-4b/Qwen3-4B-Q4_K_M.gguf' "$work/out2"; then
  pass 'a wrong SHA-256 is rejected and nothing is renamed into place'
else
  fail 'a wrong SHA-256 is rejected and nothing is renamed into place'; ls -R "$bad" >&2; cat "$work/out2" >&2
fi

# 3. A rerun skips a file already checked: with the sources gone, it still succeeds without downloading.
mv "$src" "$work/src.gone"
if bash "$fetch" "$cache" "$work/manifest.json" "$work/eval.manifest" >"$work/out3" 2>&1 &&
  [ "$(grep -c '^already checked: ' "$work/out3")" -eq 4 ] && ! grep -q '^downloading' "$work/out3"; then
  pass 'a rerun skips a file already checked'
else
  fail 'a rerun skips a file already checked'; cat "$work/out3" >&2
fi
mv "$work/src.gone" "$src"

# 4. A download resumes from its .part: the first half comes from the .part, not the server. The server's copy has
# a different first half, so only a resumed download passes the check.
resume="$work/resume"
mkdir -p "$resume/qwen3-4b"
head -c 100000 "$src/model.gguf" >"$resume/qwen3-4b/Qwen3-4B-Q4_K_M.gguf.part"
cp "$src/model.gguf" "$work/model.orig"
{ head -c 100000 /dev/zero; tail -c +100001 "$work/model.orig"; } >"$src/model.gguf"
models_json "$work/resume.json" "$(sha "$work/model.orig")"
if bash "$fetch" "$resume" "$work/resume.json" "$work/eval.manifest" >"$work/out4" 2>&1 &&
  cmp -s "$resume/qwen3-4b/Qwen3-4B-Q4_K_M.gguf" "$work/model.orig"; then
  pass 'a download resumes from its .part'
else
  fail 'a download resumes from its .part'; cat "$work/out4" >&2
fi
cp "$work/model.orig" "$src/model.gguf"

# 5. A malformed manifest is refused before anything is downloaded.
printf 'tts/x.tar.bz2 file://%s nothex\n' "$src/tool.tar.gz" >"$work/malformed.manifest"
if ! bash "$fetch" "$work/malformed" "$work/manifest.json" "$work/malformed.manifest" >"$work/out5" 2>&1 &&
  [ ! -e "$work/malformed" ]; then
  pass 'a malformed manifest is refused before anything is downloaded'
else
  fail 'a malformed manifest is refused before anything is downloaded'; cat "$work/out5" >&2
fi

if [ "$failures" -gt 0 ]; then
  echo "$failures failed" >&2
  exit 1
fi
echo 'all passed'
