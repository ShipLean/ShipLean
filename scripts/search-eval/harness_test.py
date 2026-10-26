import json
import os
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

import pytest
import requests

import metrics
import validate

HERE = Path(__file__).resolve().parent
SEARCH_SCRIPT = HERE.parent / "search-eval.py"


def cli():
    import importlib.util
    spec = importlib.util.spec_from_file_location("search_eval_cli", SEARCH_SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Stub:
    """Serves /api/search from a canned body; records the requests it saw."""

    def __init__(self, status, body):
        self.seen = []
        stub = self

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                stub.seen.append(urlparse(self.path))
                payload = json.dumps(body).encode()
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def log_message(self, *args):
                pass

        self.server = HTTPServer(("127.0.0.1", 0), Handler)
        self.url = f"http://127.0.0.1:{self.server.server_port}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()


@pytest.fixture
def results(tmp_path):
    (tmp_path / "refmap.json").write_text(json.dumps({"sample-004": "DEMO-4", "sample-002": "DEMO-2"}))
    return tmp_path


def write_queries(tmp_path):
    d = tmp_path / "q"
    d.mkdir()
    (d / "sample.json").write_text(json.dumps([
        {"id": "sample-q01", "text": "login broken", "kind": "paraphrase", "expected": ["sample-004"]},
        {"id": "sample-q02", "text": "acc", "kind": "prefix", "expected": ["sample-002"]}]))
    return d


def test_run_maps_keys_to_refs(results, tmp_path):
    fixture = json.loads((HERE / "fixtures" / "search-response.json").read_text())
    stub = Stub(200, fixture)
    try:
        code = cli().main(["run", "--base-url", stub.url, "--label", "stub", "--prefix",
                           "--queries-dir", str(write_queries(tmp_path)), "--results-dir", str(results)])
    finally:
        stub.close()
    assert code == 0
    out = json.loads((results / "stub.json").read_text())
    assert out["label"] == "stub"
    assert out["queries"] == {"sample-q01": [["sample-004", None], ["sample-002", None]],
                              "sample-q02": [["sample-004", None], ["sample-002", None]]}
    assert set(out["latencyMs"]) == {"sample-q01", "sample-q02"}
    assert all(isinstance(v, float) and v > 0 for v in out["latencyMs"].values())
    first = parse_qs(stub.seen[0].query)
    assert stub.seen[0].path == "/api/search"
    assert first == {"q": ["login broken"], "limit": ["10"], "closed": ["true"], "prefix": ["true"]}
    queries = json.loads((tmp_path / "q" / "sample.json").read_text())
    assert metrics.score(queries, out)["all"][0] == 2


def test_run_omits_prefix_unless_asked(results, tmp_path):
    stub = Stub(200, json.loads((HERE / "fixtures" / "search-response.json").read_text()))
    try:
        cli().main(["run", "--base-url", stub.url, "--label", "plain",
                    "--queries-dir", str(write_queries(tmp_path)), "--results-dir", str(results)])
    finally:
        stub.close()
    assert "prefix" not in parse_qs(stub.seen[0].query)


def test_run_reports_http_error_and_exits_nonzero(results, tmp_path, capsys):
    stub = Stub(500, {"error": "boom"})
    try:
        code = cli().main(["run", "--base-url", stub.url, "--label", "bad",
                           "--queries-dir", str(write_queries(tmp_path)), "--results-dir", str(results)])
    finally:
        stub.close()
    assert code != 0
    assert "500" in capsys.readouterr().err
    assert not (results / "bad.json").exists()


@pytest.mark.live
def test_import_sample_creates_every_item(tmp_path):
    base, email, password = (os.environ[k] for k in ("SEARCH_EVAL_URL", "SEARCH_EVAL_EMAIL", "SEARCH_EVAL_PASSWORD"))
    corpus = validate.load(HERE / "sample" / "corpus.json")
    assert cli().main(["import", "--base-url", base, "--email", email, "--password", password,
                       "--sample", "--results-dir", str(tmp_path)]) == 0
    refmap = json.loads((tmp_path / "refmap.json").read_text())
    key = corpus["project"]["key"]
    assert refmap == {item["ref"]: f"{key}-{n}" for n, item in enumerate(corpus["items"], start=1)}

    http = requests.Session()
    http.headers["X-ShipLean-Client"] = "search-eval-test"
    http.post(f"{base}/api/auth/login", json={"email": email, "password": password}).raise_for_status()
    pid = next(p["id"] for p in http.get(f"{base}/api/projects").json() if p["key"] == key)
    assert http.get(f"{base}/api/projects/{pid}/settings").json()["general"]["description"] == corpus["project"]["description"]
    parent_key = {item["ref"]: refmap[item["parentRef"]] for item in corpus["items"] if item.get("parentRef")}
    for n, item in enumerate(corpus["items"], start=1):
        got = http.get(f"{base}/api/projects/{pid}/work-items/by-key/{key}-{n}").json()
        assert got["title"] == item["title"]
        assert got["state"]["name"] == item["state"]
        assert sorted(got["tags"]) == sorted(item["tags"])
        assert [c["text"] for c in got["acceptanceCriteria"]] == item["acceptanceCriteria"]
        assert got["reproSteps"] == item["reproSteps"]
        assert (got["parent"] or {}).get("key") == parent_key.get(item["ref"])
        assert (got["sprint"] or {}).get("name") == next(
            (s["name"] for s in corpus["sprints"] if s["ref"] == item.get("sprintRef")), None)
        shown = [c["body"] for c in http.get(f"{base}/api/work-items/{got['id']}/comments").json()]
        assert shown == [c["body"] for c in item["comments"] if not c["archived"]]
