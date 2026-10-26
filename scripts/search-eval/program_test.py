import subprocess
from pathlib import Path

import pytest

import metrics

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "spikes" / "search-embeddings"


@pytest.fixture(scope="module")
def run():
    subprocess.run(["dotnet", "build", str(PROJECT), "-c", "Release", "-nologo", "-v", "q"], check=True, timeout=300)

    def go(*args):
        return subprocess.run(["dotnet", "run", "--no-build", "-c", "Release", "--project", str(PROJECT), "--", *args],
                              capture_output=True, text=True, timeout=120)
    return go


def test_unknown_flag_fails(run):
    r = run("--ranker", "ilike", "--chunksize", "800")
    assert r.returncode == 2 and "unknown argument '--chunksize'" in r.stderr


def test_flag_without_value_fails(run, tmp_path):
    r = run("--ranker", "ilike", "--out")
    assert r.returncode == 2 and "needs a value" in r.stderr


def test_bad_number_fails(run):
    r = run("--ranker", "ilike", "--chunk-size", "big")
    assert r.returncode == 2 and "--chunk-size" in r.stderr


def test_corpus_directory_defaults_to_sibling_queries(run, tmp_path):
    (tmp_path / "corpus").mkdir()
    (tmp_path / "queries").mkdir()
    sample = ROOT / "scripts" / "search-eval" / "sample"
    (tmp_path / "corpus" / "demo.json").write_text((sample / "corpus.json").read_text(encoding="utf-8"), encoding="utf-8")
    (tmp_path / "queries" / "demo.json").write_text((sample / "queries.json").read_text(encoding="utf-8"), encoding="utf-8")
    out = tmp_path / "out.json"
    r = run("--ranker", "ilike", "--corpus", str(tmp_path / "corpus"), "--out", str(out))
    assert r.returncode == 0, r.stderr
    assert metrics.main([str(out), "--queries", str(tmp_path / "queries")]) == 0
