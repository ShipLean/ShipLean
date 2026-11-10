#!/usr/bin/env bash
# Makes (or reuses) a conda-forge llama.cpp env for hosts where the prebuilt ubuntu-x64 llama-server can't run
# (glibc < 2.34, no libssl 3), and prints its llama-server path on stdout. Everything else goes to stderr.
#
# conda-forge has no build 10256 (the one llama.rn 0.12.9 bundles). 10333 is the nearest build that contains
# b10256's Qwen3 chat parser (llama.cpp #26252, landed between b10223 and b10256), so it parses Qwen3 replies the
# way the phone does. Its libs (libstdc++, libssl 3, libgomp, MKL) come from the env through its own RPATH and
# need only glibc 2.17.
#
# Usage: bash tools/voice-eval/llama-server-conda.sh [cache dir, default ~/.cache/shiplean-voice]
set -euo pipefail

VERSION=10333
BUILD='cpu_mkl_*'
cache=${1:-$HOME/.cache/shiplean-voice}
env_dir=$cache/llama-conda
micromamba=${MICROMAMBA:-$HOME/.local/micromamba/bin/micromamba}
export MAMBA_ROOT_PREFIX=${MAMBA_ROOT_PREFIX:-$HOME/.local/micromamba}
server=$env_dir/bin/llama-server

if ! ls "$env_dir/conda-meta/llama.cpp-$VERSION-"cpu_mkl_*.json >/dev/null 2>&1; then
  [ -x "$micromamba" ] || { echo "no micromamba at $micromamba (set MICROMAMBA)" >&2; exit 1; }
  echo "installing conda-forge llama.cpp $VERSION ($BUILD) into $env_dir" >&2
  if [ -d "$env_dir/conda-meta" ]; then
    "$micromamba" install -y -q -p "$env_dir" -c conda-forge --override-channels "llama.cpp=$VERSION=$BUILD" >&2
  else
    "$micromamba" create -y -q -p "$env_dir" -c conda-forge --override-channels "llama.cpp=$VERSION=$BUILD" >&2
  fi
fi

if missing=$(ldd "$server" | grep 'not found'); then
  echo "llama-server is missing libraries:" >&2
  echo "$missing" >&2
  exit 1
fi
"$server" --version >/dev/null 2>&1 || { echo "$server --version failed" >&2; exit 1; }
echo "$server"
