import json
import subprocess
from pathlib import Path

import pytest

import metrics

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "spikes" / "search-embeddings"
FIXTURES = Path(__file__).resolve().parent / "fixtures"


@pytest.fixture(scope="module")
def run():
    subprocess.run(["dotnet", "build", str(PROJECT), "-c", "Release", "-nologo", "-v", "q"], check=True, timeout=300)

    def go(*args):
        return subprocess.run(["dotnet", "run", "--no-build", "-c", "Release", "--project", str(PROJECT), "--", *args],
                              capture_output=True, text=True, timeout=120)
    return go


def test_lexical_on_the_sample_writes_a_rankings_file(run, tmp_path):
    out = tmp_path / "lexical.json"
    r = run("--ranker", "lexical", "--out", str(out))
    assert r.returncode == 0, r.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["label"] == "lexical" and len(data["queries"]) == 12
    assert metrics.main([str(out)]) == 0


def test_hybrid_fuses_two_rankings_files(run, tmp_path):
    out = tmp_path / "hybrid.json"
    r = run("--ranker", "hybrid", "--inputs", f"{FIXTURES / 'sample-ilike.json'},{FIXTURES / 'sample-semantic.json'}", "--out", str(out))
    assert r.returncode == 0, r.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert len(data["queries"]) == 12 and all(isinstance(v[0][1], float) for v in data["queries"].values() if v)
    assert metrics.main([str(out)]) == 0


def test_cutoff_sweep_prints_a_row_per_cutoff(run, tmp_path):
    r = run("--ranker", "sweep", "--inputs", str(FIXTURES / "sample-semantic.json"), "--cutoffs", "0.45:0.95:0.05", "--out", str(tmp_path / "x.json"))
    assert r.returncode == 0, r.stderr
    rows = [l for l in r.stdout.splitlines() if l.startswith("| 0.") and "| all |" in l]
    assert len(rows) == 11


def test_keyfirst_puts_the_named_item_first_for_key_shaped_queries(run, tmp_path):
    out = tmp_path / "keyfirst.json"
    r = run("--ranker", "keyfirst", "--inputs", str(FIXTURES / "sample-ilike.json"), "--out", str(out))
    assert r.returncode == 0, r.stderr
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["queries"]["sample-q01"][0][0] == "sample-004"  # DEMO-4
    assert data["queries"]["sample-q09"][0][0] == "sample-015"  # demo15, no dash
    assert data["queries"]["sample-q02"] == json.loads((FIXTURES / "sample-ilike.json").read_text(encoding="utf-8"))["queries"]["sample-q02"]


def test_lexical_or_ors_the_body_words_and_keeps_titles_anded(run, tmp_path):
    a, b = tmp_path / "and.json", tmp_path / "or.json"
    assert run("--ranker", "lexical", "--out", str(a)).returncode == 0
    r = run("--ranker", "lexical-or", "--out", str(b))
    assert r.returncode == 0, r.stderr
    data = json.loads(b.read_text(encoding="utf-8"))
    assert data["label"] == "lexical-or" and len(data["queries"]) == 12
    ands = json.loads(a.read_text(encoding="utf-8"))["queries"]
    assert sum(len(v) for v in data["queries"].values()) >= sum(len(v) for v in ands.values())
