"""Runs a screen pack through Qwen3 on this host's CPU, in chunks that stop before --max-seconds and resume.

  text:  each row's userTurn (the phone's own, from the pack) goes to llama-server with the pack's system prompt and
         grammar, thinking off, temperature 0, the prefix cached.
  audio: each utterance is synthesised once (a cached 16 kHz WAV), Parakeet transcribes it with the pack's hotwords,
         and the transcript takes the text path.

Raw outputs and timings are appended to <out>/runs/<date>/<tier>-<model>-<screen>.jsonl; rows already there are
skipped, so rerunning the same command carries on. score.ts reads them. Python never parses a call or cuts a note:
the prompt, grammar, user turn and hotwords text all come from the phone's TypeScript. RUN.md has the commands.
"""

from __future__ import annotations

import argparse
import datetime
import gc
import hashlib
import json
import os
import platform
import random
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import wave
from pathlib import Path
from typing import Callable, Iterator

HERE = Path(__file__).resolve().parent
MOBILE = HERE.parent.parent
MIN_AVAILABLE_MB = 3 * 1024
# Exit code when the chunk stopped with rows left: rerun the same command to carry on.
EXIT_MORE = 3
SAMPLE_RATE = 16000
MAX_TOKENS = 512


class NotEnoughMemory(RuntimeError):
    pass


def available_mb(run: Callable[..., subprocess.CompletedProcess] = subprocess.run) -> int:
    """The "available" column of `free -m`'s Mem: line."""
    out = run(["free", "-m"], capture_output=True, text=True, check=True).stdout
    header, mem = out.splitlines()[0].split(), next(l for l in out.splitlines() if l.startswith("Mem:")).split()
    return int(mem[1 + header.index("available")])


def require_memory(what: str, available: Callable[[], int]) -> None:
    mb = available()
    if mb < MIN_AVAILABLE_MB:
        raise NotEnoughMemory(
            f"not loading {what}: free -m shows {mb} MB available, under {MIN_AVAILABLE_MB} MB. "
            "Stop other model runs or test jobs and rerun."
        )


def cpu_name() -> str:
    try:
        for line in Path("/proc/cpuinfo").read_text().splitlines():
            if line.startswith("model name"):
                return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return platform.processor() or platform.machine()


def manifest_file(cache: Path, manifest: list[dict], file_id: str) -> Path:
    """Where fetch-models.sh put a manifest file: <cache>/<model>/<fileName>, the phone's layout."""
    entry = next((e for e in manifest if e["id"] == file_id), None)
    if entry is None:
        raise SystemExit(f"{file_id} isn't in the manifest")
    path = cache / entry["model"] / entry["fileName"]
    if not path.is_file():
        raise SystemExit(f"{path} is missing: run fetch-models.sh first (RUN.md)")
    return path


def model_gguf(cache: Path, manifest: list[dict], model: str) -> Path:
    ids = [e["id"] for e in manifest if e["model"] == model and e["fileName"].endswith(".gguf")]
    if len(ids) != 1:
        raise SystemExit(f"the manifest has {len(ids)} GGUF files for {model}")
    return manifest_file(cache, manifest, ids[0])


def find_llama_server(cache: Path) -> Path:
    found = sorted(cache.glob("llama.cpp/llama-*-bin-ubuntu-x64/*/llama-server"))
    if not found:
        raise SystemExit(f"no llama-server under {cache}/llama.cpp: run fetch-models.sh, or pass --llama-server")
    return found[-1]


class LlamaServer:
    """One llama-server process on a free local port, stopped on exit."""

    def __init__(self, binary: Path, gguf: Path, threads: int, log: Path):
        self.binary, self.gguf, self.threads, self.log = binary, gguf, threads, log
        self.proc: subprocess.Popen | None = None
        self.url = ""

    def start(self, timeout: float) -> None:
        with socket.socket() as s:
            s.bind(("127.0.0.1", 0))
            port = s.getsockname()[1]
        self.url = f"http://127.0.0.1:{port}"
        self.log.parent.mkdir(parents=True, exist_ok=True)
        cmd = [str(self.binary), "-m", str(self.gguf), "--jinja", "-t", str(self.threads), "-c", "4096", "-np", "1",
               "--host", "127.0.0.1", "--port", str(port)]
        env = {**os.environ, "LD_LIBRARY_PATH": f"{self.binary.parent}:{os.environ.get('LD_LIBRARY_PATH', '')}"}
        with open(self.log, "ab") as log:
            self.proc = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT, env=env)
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if self.proc.poll() is not None:
                raise SystemExit(f"llama-server exited with {self.proc.returncode}; see {self.log}")
            try:
                with urllib.request.urlopen(f"{self.url}/health", timeout=2) as r:
                    if r.status == 200:
                        return
            except (urllib.error.URLError, OSError):
                pass
            time.sleep(0.5)
        raise TimeoutError("llama-server didn't load in time")

    def stop(self) -> None:
        if self.proc is not None and self.proc.poll() is None:
            self.proc.terminate()
            try:
                self.proc.wait(timeout=20)
            except subprocess.TimeoutExpired:
                self.proc.kill()
                self.proc.wait()


def request_body(system: str, user: str, grammar: str) -> dict:
    return {
        "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
        "grammar": grammar,
        "temperature": 0,
        "max_tokens": MAX_TOKENS,
        "cache_prompt": True,
        "chat_template_kwargs": {"enable_thinking": False},
    }


def complete(url: str, body: dict, timeout: float) -> str:
    req = urllib.request.Request(
        f"{url}/v1/chat/completions", data=json.dumps(body).encode(), headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.load(r)["choices"][0]["message"]["content"]


def heard_turn(row: dict, transcript: str) -> str:
    """The pack's user turn with Parakeet's transcript in place of the utterance.

    The phone's userTurn puts the transcript last, word for word, so the pack's turn ends with the utterance; only
    that tail is swapped and the screen context before it is sent as the pack has it."""
    turn, utterance = row["userTurn"], row["utterance"]
    if not turn.endswith(utterance):
        raise SystemExit("the pack's userTurn doesn't end with the utterance: re-export the packs")
    return turn[: len(turn) - len(utterance)] + transcript


def select_rows(count: int, sample: int | None, seed: int) -> list[int]:
    if sample is None or sample >= count:
        return list(range(count))
    return sorted(random.Random(seed).sample(range(count), sample))


def done_rows(path: Path) -> set[tuple[int, str]]:
    if not path.exists():
        return set()
    done = set()
    for text in path.read_text().splitlines():
        if text.strip():
            line = json.loads(text)
            done.add((line["row"], line["utterance"]))
    return done


class Speech:
    """Synthesised audio and Parakeet transcripts, both cached under <out>/audio/. Loads one model at a time."""

    def __init__(self, cache: Path, manifest: list[dict], out: Path, threads: int, available: Callable[[], int]):
        self.cache, self.manifest, self.dir, self.threads, self.available = cache, manifest, out / "audio", threads, available
        self.dir.mkdir(parents=True, exist_ok=True)

    def wav(self, utterance: str) -> Path:
        return self.dir / f"{hashlib.sha256(utterance.encode()).hexdigest()[:20]}.wav"

    def transcribe(self, rows: list[dict], hotwords_text: str, deadline: float, clock: Callable[[], float]) -> Iterator[tuple[int, str, int]]:
        """Yields (row index, transcript, ms) per row, stopping at the deadline. Transcripts are cached per hotwords."""
        tag = hashlib.sha256(hotwords_text.encode()).hexdigest()[:8]
        cached = {}
        todo = []
        for i, row in rows:
            path = self.wav(row["utterance"]).with_suffix(f".{tag}.json")
            if path.exists():
                cached[i] = json.loads(path.read_text())
            else:
                todo.append((i, row, path))
        for i, c in cached.items():
            yield i, c["text"], c["ms"]
        missing = [row["utterance"] for _, row, _ in todo if not self.wav(row["utterance"]).exists()]
        if missing:
            self._synthesise(missing, deadline, clock)
        if not todo:
            return
        recognizer = self._recognizer(hotwords_text)
        import numpy as np
        for i, row, path in todo:
            wav = self.wav(row["utterance"])
            if clock() >= deadline or not wav.exists():
                break
            with wave.open(str(wav)) as w:
                samples = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
            start = clock()
            stream = recognizer.create_stream()
            stream.accept_waveform(SAMPLE_RATE, samples)
            recognizer.decode_stream(stream)
            ms = round((clock() - start) * 1000)
            path.write_text(json.dumps({"text": stream.result.text, "ms": ms}))
            yield i, stream.result.text, ms

    def _synthesise(self, utterances: list[str], deadline: float, clock: Callable[[], float]) -> None:
        import numpy as np
        import sherpa_onnx
        require_memory("the TTS voice", self.available)
        voice = next(self.cache.glob("tts/vits-piper-*/*.onnx"))
        tts = sherpa_onnx.OfflineTts(sherpa_onnx.OfflineTtsConfig(model=sherpa_onnx.OfflineTtsModelConfig(
            vits=sherpa_onnx.OfflineTtsVitsModelConfig(
                model=str(voice), tokens=str(voice.parent / "tokens.txt"), data_dir=str(voice.parent / "espeak-ng-data")),
            num_threads=self.threads)))
        for utterance in utterances:
            if clock() >= deadline:
                break
            audio = tts.generate(utterance, sid=0, speed=1.0)
            samples = np.asarray(audio.samples, dtype=np.float32)
            if audio.sample_rate != SAMPLE_RATE:
                at = np.arange(0, len(samples), audio.sample_rate / SAMPLE_RATE)
                samples = np.interp(at, np.arange(len(samples)), samples).astype(np.float32)
            pcm = (np.clip(samples, -1, 1) * 32767).astype(np.int16)
            part = self.wav(utterance).with_suffix(".part")
            with wave.open(str(part), "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(SAMPLE_RATE)
                w.writeframes(pcm.tobytes())
            part.rename(self.wav(utterance))
        del tts
        gc.collect()

    def _recognizer(self, hotwords_text: str):
        import sherpa_onnx
        require_memory("Parakeet", self.available)
        f = lambda file_id: str(manifest_file(self.cache, self.manifest, file_id))
        tokens = Path(f("parakeet-tokens"))
        # sherpa-onnx encodes hotwords with modeling_unit bpe and a vocab built from tokens.txt, one line per token:
        # "<piece>\t-<id>", as the phone writes it next to its model files. Here it goes in <out>/audio, never the cache.
        vocab = self.dir / "parakeet-bpe.vocab"
        vocab.write_text("".join(f"{p}\t-{i}\n" for p, i in (l.rsplit(" ", 1) for l in tokens.read_text().splitlines() if l)))
        hotwords = self.dir / f"hotwords-{hashlib.sha256(hotwords_text.encode()).hexdigest()[:8]}.txt"
        hotwords.write_text(hotwords_text + "\n")
        return sherpa_onnx.OfflineRecognizer.from_transducer(
            encoder=f("parakeet-encoder"), decoder=f("parakeet-decoder"), joiner=f("parakeet-joiner"), tokens=str(tokens),
            num_threads=self.threads, sample_rate=SAMPLE_RATE, decoding_method="modified_beam_search",
            hotwords_file=str(hotwords), modeling_unit="bpe", bpe_vocab=str(vocab), model_type="nemo_transducer")


def hotwords_text(pack_path: Path) -> str:
    """The phone's own hotwords file for the pack (toHotwords in TypeScript), never a Python copy of it."""
    cmd = ["node", "--import", "./tools/voice-eval/ts-hooks.mjs", "--disable-warning=MODULE_TYPELESS_PACKAGE_JSON",
           "tools/voice-eval/hotwords-file.ts", str(pack_path)]
    return subprocess.run(cmd, cwd=MOBILE, capture_output=True, text=True, check=True).stdout


def run(argv: list[str], *, server_factory=LlamaServer, speech_factory=Speech, hotwords=hotwords_text,
        available: Callable[[], int] = available_mb, clock: Callable[[], float] = time.monotonic,
        send: Callable[[str, dict, float], str] = complete) -> int:
    p = argparse.ArgumentParser(prog="run.py")
    p.add_argument("tier", choices=["text", "audio"])
    p.add_argument("--model", required=True, choices=["qwen3-1.7b", "qwen3-4b"])
    p.add_argument("--screen", required=True)
    p.add_argument("--cache", required=True, type=Path)
    p.add_argument("--packs", type=Path, default=HERE / "out", help="where export.ts wrote <screen>.json and manifest.json")
    p.add_argument("--out", type=Path, default=None, help="runs go to <out>/runs/<date>/ (default: --packs)")
    p.add_argument("--max-seconds", type=float, default=240)
    p.add_argument("--sample", type=int, default=None)
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--threads", type=int, default=4)
    p.add_argument("--date", default=None, help="the runs folder to resume (default: today, UTC)")
    p.add_argument("--llama-server", type=Path, default=None)
    a = p.parse_args(argv)

    started = clock()
    deadline = started + a.max_seconds
    out = a.out or a.packs
    pack_path = a.packs / f"{a.screen}.json"
    pack = json.loads(pack_path.read_text())
    manifest = json.loads((a.packs / "manifest.json").read_text())
    date = a.date or datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d")
    run_file = out / "runs" / date / f"{a.tier}-{a.model}-{a.screen}.jsonl"
    run_file.parent.mkdir(parents=True, exist_ok=True)

    rows = pack["rows"]
    chosen = select_rows(len(rows), a.sample, a.seed)
    done = done_rows(run_file)
    todo = [i for i in chosen if (i, rows[i]["utterance"]) not in done]
    print(f"{a.tier} {a.model} {a.screen}: {len(chosen) - len(todo)} of {len(chosen)} rows done before this chunk")
    if not todo:
        print(f"all done: {run_file}")
        return 0

    # The model and the server binary are found before any audio work, so a missing file fails at once.
    gguf = model_gguf(a.cache, manifest, a.model)
    binary = a.llama_server or find_llama_server(a.cache)
    heard: dict[int, tuple[str, int]] = {}
    if a.tier == "audio":
        speech = speech_factory(a.cache, manifest, out, a.threads, available)
        for i, text, ms in speech.transcribe([(i, rows[i]) for i in todo], hotwords(pack_path), deadline, clock):
            heard[i] = (text, ms)
        gc.collect()
        todo = [i for i in todo if i in heard]

    require_memory(a.model, available)
    server = server_factory(binary, gguf, a.threads, out / "runs" / date / "llama-server.log")
    written = 0
    slowest = 0.0
    cpu = cpu_name()
    try:
        server.start(timeout=max(1.0, deadline - clock()))
        with open(run_file, "a") as sink:
            for i in todo:
                left = deadline - clock()
                if left <= slowest:
                    break
                row = rows[i]
                transcript, stt_ms = heard.get(i, (row["utterance"], None))
                user = row["userTurn"] if a.tier == "text" else heard_turn(row, transcript)
                start = clock()
                try:
                    output = send(server.url, request_body(pack["systemPrompt"], user, pack["grammar"]), left)
                except TimeoutError:
                    break
                took = clock() - start
                slowest = max(slowest, took)
                sink.write(json.dumps({
                    "row": i, "screen": pack["screen"], "utterance": row["utterance"], "tier": a.tier, "model": a.model,
                    "transcript": transcript, "output": output, "llmMs": round(took * 1000), "sttMs": stt_ms,
                    "expectedCalls": row["calls"], "expectedNote": row["note"], "cpu": cpu, "threads": a.threads,
                }) + "\n")
                sink.flush()
                written += 1
    finally:
        server.stop()

    left = len(chosen) - len(done & {(i, rows[i]["utterance"]) for i in chosen}) - written
    print(f"wrote {written} rows to {run_file} in {clock() - started:.0f} s; {left} left")
    if left:
        print("rerun the same command (with --date " + date + " after midnight UTC) to carry on")
        return EXIT_MORE
    return 0


def main() -> int:
    try:
        return run(sys.argv[1:])
    except NotEnoughMemory as e:
        print(e, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
