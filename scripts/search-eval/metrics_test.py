import json
from pathlib import Path

import metrics

HERE = Path(__file__).resolve().parent

QUERIES = [
    {"id": "a", "text": "x", "kind": "titleWord", "expected": ["r1"]},
    {"id": "b", "text": "y", "kind": "titleWord", "expected": ["r2"]},
    {"id": "c", "text": "z", "kind": "paraphrase", "expected": ["r3"]},
]


def test_known_rankings_give_known_scores():
    rankings = {"label": "t", "queries": {
        "a": [["r1", None]],
        "b": [["r9", None], ["r2", None]],
        "c": [["r9", None], ["r8", None], ["r7", None], ["r6", None], ["r5", None], ["r3", None]],
    }, "latencyMs": {}}
    scores = metrics.score(QUERIES, rankings)
    assert scores["titleWord"] == (2, 0.5, 1.0, 0.75)
    assert scores["paraphrase"] == (1, 0.0, 0.0, 1 / 6)
    assert scores["all"][0] == 3


def test_missing_query_counts_as_miss():
    scores = metrics.score(QUERIES, {"label": "t", "queries": {"a": [["r1", 0.9]]}, "latencyMs": {}})
    assert scores["titleWord"] == (2, 0.5, 0.5, 0.5)
    assert scores["paraphrase"] == (1, 0.0, 0.0, 0.0)


def test_unscored_rankings_are_accepted():
    rankings = {"label": "t", "queries": {"a": [["r1", None]]}, "latencyMs": {}}
    assert "| t | titleWord | 2 |" in metrics.table(QUERIES, [rankings])


def test_sample_fixture_prints_a_table(capsys):
    assert metrics.main([str(HERE / "fixtures" / "sample-semantic.json")]) == 0
    assert "| sample-semantic | all | 12 |" in capsys.readouterr().out


def test_ilike_fixture_is_readable():
    ilike = json.loads((HERE / "fixtures" / "sample-ilike.json").read_text(encoding="utf-8"))
    assert ilike["label"] == "ilike"
    assert "| ilike | all | 12 |" in metrics.table(
        json.loads((HERE / "sample" / "queries.json").read_text(encoding="utf-8")), [ilike])


def test_table_reports_counted_query_files(capsys):
    metrics.main([str(HERE / "fixtures" / "sample-semantic.json")])
    assert "Query files counted: queries.json." in capsys.readouterr().out
