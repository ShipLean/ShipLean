# Running the voice eval on the host

`run.py` sends a screen pack through Qwen3 on this machine's CPU and appends the raw replies and timings to a run
file. `score.ts` turns the run files into a Markdown report. Python never builds a prompt, parses a call or cuts a
note: the system prompt, grammar and user turns come from the pack (`export.ts`), the hotwords text from the phone's
`toHotwords` (`hotwords-file.ts`), and scoring uses the phone's `parseModelOutput` and `copyNoteSpan`.

Everything it writes stays outside the repo. The commands below use `~/.cache/shiplean-voice` as the cache (models,
llama.cpp, the TTS voice and the venv) and `~/.cache/shiplean-voice/eval-out` for packs, runs and reports.

## Once: the venv, the packs and the models

```bash
cd src/ShipLean.Mobile
C=~/.cache/shiplean-voice
uv venv $C/eval-venv --python 3.12
uv pip install -p $C/eval-venv -r tools/voice-eval/requirements.txt
node --import ./tools/voice-eval/ts-hooks.mjs --disable-warning=MODULE_TYPELESS_PACKAGE_JSON \
  tools/voice-eval/export.ts --out $C/eval-out
df -h $C        # Qwen3 1.7B is 1.1 GB, Qwen3 4B 2.5 GB
bash tools/voice-eval/fetch-models.sh $C $C/eval-out/manifest.json
```

Never `pip install` into the system Python. A fetch that stops (each run is cut at 5 minutes on a shared host) is
resumed by rerunning the same command.

## Run a chunk

```bash
free -m          # "available" must be at least 3072 MB, or run.py refuses to load a model
timeout 300 $C/eval-venv/bin/python -I tools/voice-eval/run.py text --model qwen3-1.7b --screen features \
  --cache $C --packs $C/eval-out --max-seconds 240 --threads 4
```

- `text` sends each row's `userTurn` as it is. `audio` first synthesises each utterance (Piper voice, a 16 kHz WAV
  cached in `<packs>/audio/`), transcribes it with Parakeet and the pack's hotwords (transcripts cached per hotwords
  list), then sends the pack's user turn with the transcript in place of the utterance.
- One model is loaded at a time: the TTS voice, then Parakeet, then one `llama-server`, which is stopped when the
  chunk ends. Each load checks `free -m` first. Use `--threads 4` (or fewer) when other agents share the host.
- The request is `/v1/chat/completions` with the pack's grammar, `temperature: 0`, `cache_prompt: true` and
  `chat_template_kwargs: {"enable_thinking": false}`, on `llama-server --jinja`.
- `--sample N --seed 1` takes the same N rows on every rerun. `--llama-server <path>` uses another binary.

## Resume

A chunk stops before `--max-seconds` (it never starts a row it can't finish in the time left, judged by the slowest
row so far) and exits 3 with the number of rows left. Rerun the same command: rows already in the run file are
skipped. Runs go to `<packs>/runs/<date>/<tier>-<model>-<screen>.jsonl`, `<date>` being today in UTC, so after
midnight UTC add `--date <yyyy-mm-dd>` to carry on the same run. Exit 0 means every chosen row is done; exit 2 means
there wasn't enough memory.

## Score

```bash
node --import ./tools/voice-eval/ts-hooks.mjs --disable-warning=MODULE_TYPELESS_PACKAGE_JSON \
  tools/voice-eval/score.ts $C/eval-out/runs/<date>
```

It writes `$C/eval-out/voice-eval-<date>.md` (`--out <file>` to write elsewhere): per screen, tier and model, each
action's rows, right action %, right args %, exact note % (of the rows that expect a note), and median and p90
latency (speech recognition plus the model). An action under 90 % on any of the three is flagged. The header names
the host CPU and thread count. These are CPU times on this host, not phone times.

## Known problem on this host

The prebuilt llama.cpp b10256 (`ubuntu-x64`) needs glibc 2.34 and GLIBCXX 3.4.30, and this host is Ubuntu 20.04
(glibc 2.31) with no compiler: `llama-server` doesn't start here. The text tier needs a `llama-server` b10256 that
runs on this host, passed with `--llama-server`. The audio tier's speech steps (TTS and Parakeet) run.

## Tests

```bash
timeout 300 $C/eval-venv/bin/python -I -m pytest tools/voice-eval -q
timeout 300 pnpm exec vitest run --config tools/voice-eval/vitest.config.ts tools/voice-eval/score.test.ts
```

The pytest tests fake the model (a fake clock and sender, a local HTTP server for the request body, a faked
`free -m`) on the pack in `run-fixtures/`.
