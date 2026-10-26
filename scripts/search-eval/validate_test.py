import copy
import json
from pathlib import Path

import validate

HERE = Path(__file__).resolve().parent


def sample():
    return json.loads((HERE / "sample" / "corpus.json").read_text(encoding="utf-8"))


def item(corpus, ref):
    return next(i for i in corpus["items"] if i["ref"] == ref)


def test_sample_is_valid():
    corpus = sample()
    queries = json.loads((HERE / "sample" / "queries.json").read_text(encoding="utf-8"))
    assert validate.validate_corpus(corpus) == []
    assert validate.validate_queries(queries, corpus) == []


def test_duplicate_ref_is_rejected():
    corpus = sample()
    corpus["items"][1]["ref"] = corpus["items"][0]["ref"]
    assert any("duplicate ref" in p for p in validate.validate_corpus(corpus))


def test_duplicate_ref_across_files_is_rejected():
    seen = set()
    assert validate.validate_corpus(sample(), seen) == []
    assert any("duplicate ref" in p for p in validate.validate_corpus(sample(), seen))


def test_missing_parent_is_rejected():
    corpus = sample()
    item(corpus, "sample-003")["parentRef"] = "sample-999"
    assert any("missing parent" in p for p in validate.validate_corpus(corpus))


def test_bad_parent_type_is_rejected():
    corpus = sample()
    item(corpus, "sample-005")["parentRef"] = "sample-001"  # a task under an epic
    assert any("cannot have" in p for p in validate.validate_corpus(corpus))


def test_parent_after_child_is_rejected():
    corpus = sample()
    item(corpus, "sample-002")["parentRef"] = "sample-008"  # a parent must come earlier in items
    assert any("must come earlier" in p for p in validate.validate_corpus(corpus))


def test_overlapping_sprints_are_rejected():
    corpus = sample()
    corpus["sprints"][1]["startDate"] = "2026-01-10"
    assert any("overlap" in p for p in validate.validate_corpus(corpus))


def test_seeded_key_is_rejected():
    corpus = sample()
    corpus["project"]["key"] = "SHIP"
    assert any("seeded" in p for p in validate.validate_corpus(corpus))


def test_unknown_state_is_rejected():
    corpus = sample()
    item(corpus, "sample-003")["state"] = "Done"
    assert validate.validate_corpus(corpus) != []


def queries():
    return json.loads((HERE / "sample" / "queries.json").read_text(encoding="utf-8"))


def test_query_ids_must_be_unique_across_files():
    seen = set()
    assert validate.validate_queries(queries(), sample(), seen) == []
    assert any("duplicate query id" in p for p in validate.validate_queries(queries(), sample(), seen))


def test_query_id_needs_project_prefix():
    qs = queries()
    qs[0]["id"] = "q01"
    assert any("must start with" in p for p in validate.validate_queries(qs, sample()))


def test_exact_key_must_name_expected_item():
    qs = queries()
    qs[0]["text"] = "DEMO-5"  # sample-004 is DEMO-4
    assert any("exactKey" in p for p in validate.validate_queries(qs, sample()))


def test_exact_key_accepts_compact_lowercase_form():
    assert validate.item_keys(sample())["sample-015"] == "DEMO-15"
    assert [q for q in queries() if q["text"] == "demo15"]
    assert validate.validate_queries(queries(), sample()) == []


def test_project_key_must_be_unique_across_files():
    keys = set()
    assert validate.validate_corpus(sample(), set(), keys) == []
    assert any("duplicate project key" in p for p in validate.validate_corpus(sample(), set(), keys))


def test_sprint_names_differing_in_case_are_rejected():
    corpus = sample()
    corpus["sprints"][1]["name"] = "SPRINT 1"
    assert any("duplicate sprint name" in p for p in validate.validate_corpus(corpus))


def test_removed_expected_item_is_rejected():
    qs = queries()
    qs[0]["expected"] = ["sample-010"]
    qs[0]["kind"] = "titleWord"
    assert any("Removed" in p for p in validate.validate_queries(qs, sample()))
