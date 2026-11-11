"""run.py with the model faked: no llama-server, no download. Run: <venv>/bin/python -I -m pytest tools/voice-eval -q"""

import json
import shutil
import subprocess
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

import run

FIXTURES = Path(__file__).resolve().parent / "run-fixtures"
PACK = json.loads((FIXTURES / "fixture.json").read_text())
ROWS = len(PACK["rows"])


class Clock:
    def __init__(self):
        self.now = 1000.0

    def __call__(self):
        return self.now


class FakeServer:
    """Stands in for the llama-server process; `url` is wherever the requests go."""

    def __init__(self, url="http://fake"):
        self.url, self.started, self.stopped = url, 0, 0

    def start(self, timeout):
        self.started += 1

    def stop(self):
        self.stopped += 1


class Sends:
    """A fake model call: records each request and takes `seconds` of the fake clock."""

    def __init__(self, clock, seconds=10.0):
        self.clock, self.seconds, self.bodies, self.timeouts = clock, seconds, [], []

    def __call__(self, url, body, timeout):
        self.bodies.append(body)
        self.timeouts.append(timeout)
        self.clock.now += self.seconds
        return "[]"


@pytest.fixture
def dirs(tmp_path):
    packs = tmp_path / "packs"
    packs.mkdir()
    shutil.copy(FIXTURES / "fixture.json", packs / "fixture.json")
    shutil.copy(FIXTURES / "manifest.json", packs / "manifest.json")
    cache = tmp_path / "cache"
    (cache / "qwen3-1.7b").mkdir(parents=True)
    (cache / "qwen3-1.7b" / "Qwen3-1.7B-Q4_K_M.gguf").write_bytes(b"")
    return packs, cache


def args(packs, cache, *extra):
    return ["text", "--model", "qwen3-1.7b", "--screen", "fixture", "--cache", str(cache), "--packs", str(packs),
            "--date", "2026-11-10", "--llama-server", "/nonexistent/llama-server", *extra]


def run_lines(packs):
    path = packs / "runs" / "2026-11-10" / "text-qwen3-1.7b-fixture.jsonl"
    return [json.loads(l) for l in path.read_text().splitlines()]


def plenty():
    return 6000


def test_run_resumes_and_skips_done_rows(dirs):
    packs, cache = dirs
    clock = Clock()
    first = Sends(clock)
    server = FakeServer()
    code = run.run(args(packs, cache, "--max-seconds", "35"), server_factory=lambda *a: server, available=plenty,
                   clock=clock, send=first)
    assert code == run.EXIT_MORE
    assert len(first.bodies) == 3

    second = Sends(clock)
    code = run.run(args(packs, cache, "--max-seconds", "600"), server_factory=lambda *a: server, available=plenty,
                   clock=clock, send=second)
    assert code == 0
    assert [b["messages"][1]["content"] for b in second.bodies] == [r["userTurn"] for r in PACK["rows"][3:]]
    assert [l["row"] for l in run_lines(packs)] == list(range(ROWS))

    third = Sends(clock)
    assert run.run(args(packs, cache), server_factory=lambda *a: server, available=plenty, clock=clock, send=third) == 0
    assert third.bodies == []
    assert server.stopped == 2


def test_run_stops_at_max_seconds(dirs):
    packs, cache = dirs
    clock = Clock()
    start = clock.now
    sends = Sends(clock)
    server = FakeServer()
    code = run.run(args(packs, cache, "--max-seconds", "35"), server_factory=lambda *a: server, available=plenty,
                   clock=clock, send=sends)
    assert code == run.EXIT_MORE
    assert clock.now - start <= 35
    assert len(sends.bodies) == 3
    assert all(t <= 35 for t in sends.timeouts)
    assert len(run_lines(packs)) == 3
    assert server.stopped == 1


class Recorder(BaseHTTPRequestHandler):
    bodies: list = []

    def do_POST(self):
        Recorder.bodies.append((self.path, json.loads(self.rfile.read(int(self.headers["Content-Length"])))))
        reply = json.dumps({"choices": [{"message": {"role": "assistant", "content": "[]"}}]}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(reply)))
        self.end_headers()
        self.wfile.write(reply)

    def log_message(self, *a):
        pass


def test_run_sends_the_pack_grammar_with_thinking_off(dirs):
    packs, cache = dirs
    Recorder.bodies = []
    http = ThreadingHTTPServer(("127.0.0.1", 0), Recorder)
    threading.Thread(target=http.serve_forever, daemon=True).start()
    try:
        server = FakeServer(f"http://127.0.0.1:{http.server_address[1]}")
        assert run.run(args(packs, cache, "--sample", "2"), server_factory=lambda *a: server, available=plenty) == 0
    finally:
        http.shutdown()

    assert len(Recorder.bodies) == 2
    for path, body in Recorder.bodies:
        assert path == "/v1/chat/completions"
        assert body["grammar"] == PACK["grammar"]
        assert body["chat_template_kwargs"] == {"enable_thinking": False}
        assert body["temperature"] == 0
        assert body["cache_prompt"] is True
        assert body["messages"][0] == {"role": "system", "content": PACK["systemPrompt"]}
        assert body["messages"][1]["content"] in [r["userTurn"] for r in PACK["rows"]]
    assert [l["output"] for l in run_lines(packs)] == ["[]", "[]"]


def free_m(available, free=5200):
    out = (
        "               total        used        free      shared  buff/cache   available\n"
        f"Mem:            7638        3050        {free}          12        4176        {available}\n"
        "Swap:           2048        1040        1007\n"
    )
    return lambda cmd, **kw: subprocess.CompletedProcess(cmd, 0, stdout=out)


def test_run_refuses_to_load_with_under_3_gb_free(dirs):
    packs, cache = dirs
    made = []
    sends = Sends(Clock())
    with pytest.raises(run.NotEnoughMemory, match="2900 MB available"):
        run.run(args(packs, cache), server_factory=lambda *a: made.append(a) or FakeServer(),
                available=lambda: run.available_mb(run=free_m(2900)), send=sends)
    assert made == []
    assert sends.bodies == []

    assert run.run(args(packs, cache, "--sample", "1"), server_factory=lambda *a: made.append(a) or FakeServer(),
                   available=lambda: run.available_mb(run=free_m(3100, free=400)), send=sends) == 0
    assert len(made) == 1
