# Voice eval

Measures the command model against each screen's test set (`TestSetRow` in `src/voice/contracts.ts`): the right
action, the right arguments, the exact note text and the time taken. It runs on a workstation, not in CI.

## Export the screen packs

```bash
cd src/ShipLean.Mobile
node --import ./tools/voice-eval/ts-hooks.mjs --disable-warning=MODULE_TYPELESS_PACKAGE_JSON \
  tools/voice-eval/export.ts            # --out <dir> to write elsewhere
```

For each screen registered in `screens.ts` it writes `tools/voice-eval/out/<screen>.json`:
`{ screen, systemPrompt, grammar, hotwords, rows }`. The prompt, grammar and hotwords come from the phone's own
`buildSystemPrompt`, `buildGrammar` and the spec's `hotwords()`, so the eval runs exactly what the phone runs. Each row
is a `TestSetRow` from `src/voice/testset/<screen>.jsonl` (every file there is read; none is listed) plus two fields:
`context` (the spec's `context()` when the row has none) and `userTurn` (the phone's `userTurn` for that row).
It also writes `out/manifest.json`, the pinned model files from `src/voice/manifest.ts`.

The exporter rejects a row whose action isn't in the spec, whose arguments don't fit the action's schema, or whose
note isn't the utterance's own words at the call's `noteSpan` (as `copyNoteSpan` copies them). Rejected rows are
left out and the run exits 1. It lists every action with fewer than five rows, and any test set whose screen isn't
registered yet. `ts-hooks.mjs` lets Node run the app's TypeScript as it is: Node strips the types and the hook
resolves the extensionless imports.

## Fetch the models

Export first: the fetch reads `out/manifest.json`. Then, with about 4.5 GB free in the cache:

```bash
cd src/ShipLean.Mobile
bash tools/voice-eval/fetch-models.sh ~/.cache/shiplean-voice
```

The cache is `~/.cache/shiplean-voice`, outside the repo; nothing is ever downloaded into the worktree. Model files
land where the phone keeps them, `<cache>/<model>/<fileName>` (for example `parakeet/encoder.int8.onnx` and
`qwen3-4b/Qwen3-4B-Q4_K_M.gguf`). `eval-models.manifest` adds the eval's own tools: llama.cpp b10256 (the build
llama.rn bundles) unpacked into `<cache>/llama.cpp/llama-b10256-bin-ubuntu-x64/`, and sherpa-onnx's Piper voice for
synthesising test audio in `<cache>/tts/vits-piper-en_US-amy-low/`.

Every URL is pinned to a revision and every file is checked against its SHA-256. A download goes to `<file>.part`
and is renamed into place only once it matches, so the cache never holds a wrong file. Rerunning is safe: a lock
serialises runs, checked files are skipped, and a stopped download resumes from its `.part`. Its test needs no
network: `bash tools/voice-eval/fetch-models.test.sh`.

## Run and score

`run.py` runs a screen pack through Qwen3 on the host's CPU (text, or synthesised audio through Parakeet first), in
chunks under `--max-seconds` that resume where they stopped; `score.ts` turns the runs into a Markdown report of
right action, right args, exact note and CPU latency per screen and action. [RUN.md](RUN.md) has the commands, the
memory rules and how to resume.

The eval's own tests: `pnpm exec vitest run --config tools/voice-eval/vitest.config.ts` (TypeScript) and
`<venv>/bin/python -I -m pytest tools/voice-eval -q` (run.py, models faked).
