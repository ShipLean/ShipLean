#!/usr/bin/env python3
"""Validate search-eval corpus files (CA1) and their query files."""
import json
import re
import sys
from datetime import date
from pathlib import Path

import jsonschema

HERE = Path(__file__).resolve().parent
SEEDED_KEYS = {"SHIP", "PAY", "MOB", "DATA", "OPS", "PART", "ARCH", "EMPT"}
STATES = {"New", "Active", "Resolved", "Closed", "Removed"}
PARENT_TYPES = {"epic": set(), "feature": {"epic", "feature"}, "story": {"feature"}, "bug": {"feature"}, "task": {"story", "bug"}}
KINDS = {"exactKey", "titleWord", "paraphrase", "bodyOnly", "commentOnly", "prefix", "nonEnglish"}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def refs_of(corpus):
    yield corpus["project"]["ref"]
    for s in corpus["sprints"]:
        yield s["ref"]
    for item in corpus["items"]:
        yield item["ref"]
        for c in item["comments"]:
            yield c["ref"]


def validate_corpus(corpus, seen_refs=None, seen_keys=None):
    """Returns a list of problems (empty when valid). seen_refs and seen_keys accumulate across files."""
    schema = load(HERE / "corpus.schema.json")
    problems = [f"schema: {e.message}" for e in jsonschema.Draft202012Validator(schema).iter_errors(corpus)]
    if problems:
        return problems

    key = corpus["project"]["key"]
    if key in SEEDED_KEYS:
        problems.append(f"key {key} collides with a seeded key")
    if seen_keys is not None:
        if key in seen_keys:
            problems.append(f"duplicate project key {key}")
        seen_keys.add(key)

    seen = seen_refs if seen_refs is not None else set()
    local = set()
    for ref in refs_of(corpus):
        if ref in local or ref in seen:
            problems.append(f"duplicate ref {ref}")
        local.add(ref)
    seen.update(local)

    sprints = {s["ref"]: s for s in corpus["sprints"]}
    names = set()
    for s in corpus["sprints"]:
        if s["name"].casefold() in names:
            problems.append(f"duplicate sprint name {s['name']}")
        names.add(s["name"].casefold())
        if date.fromisoformat(s["endDate"]) < date.fromisoformat(s["startDate"]):
            problems.append(f"sprint {s['ref']} ends before it starts")
    ordered = sorted(corpus["sprints"], key=lambda s: s["startDate"])
    for a, b in zip(ordered, ordered[1:]):
        if b["startDate"] <= a["endDate"]:
            problems.append(f"sprints {a['ref']} and {b['ref']} overlap")

    items = {i["ref"]: i for i in corpus["items"]}
    position = {i["ref"]: n for n, i in enumerate(corpus["items"], 1)}
    for item in corpus["items"]:
        if "sprintRef" in item and item["sprintRef"] not in sprints:
            problems.append(f"{item['ref']}: unknown sprint {item['sprintRef']}")
        parent_ref = item.get("parentRef")
        if parent_ref is None:
            continue
        parent = items.get(parent_ref)
        if parent is None:
            problems.append(f"{item['ref']}: missing parent {parent_ref}")
        elif position[parent_ref] >= position[item["ref"]]:
            problems.append(f"{item['ref']}: parent {parent_ref} must come earlier in items")
        elif parent["type"] not in PARENT_TYPES[item["type"]]:
            problems.append(f"{item['ref']}: a {item['type']} cannot have a {parent['type']} parent")
    return problems


def item_keys(corpus):
    """ref -> key: the n-th entry of items (1-based) becomes <KEY>-n."""
    key = corpus["project"]["key"]
    return {i["ref"]: f"{key}-{n}" for n, i in enumerate(corpus["items"], 1)}


def validate_queries(queries, corpus, seen_ids=None):
    problems = []
    states = {i["ref"]: i["state"] for i in corpus["items"]}
    keys = item_keys(corpus)
    prefix = corpus["project"]["ref"] + "-"
    seen = seen_ids if seen_ids is not None else set()
    local = set()
    for q in queries:
        qid = q.get("id")
        if qid in local or qid in seen:
            problems.append(f"duplicate query id {qid}")
        local.add(qid)
        if not str(qid).startswith(prefix):
            problems.append(f"query {qid}: id must start with {prefix}")
        if q.get("kind") not in KINDS:
            problems.append(f"query {qid}: bad kind {q.get('kind')}")
        if not q.get("text") or not q.get("expected"):
            problems.append(f"query {qid}: needs text and expected")
        for r in q.get("expected", []):
            if r not in states:
                problems.append(f"query {qid}: expected {r} is not in the corpus")
            elif states[r] == "Removed":
                problems.append(f"query {qid}: expected {r} is Removed and never indexed")
        if q.get("kind") == "exactKey":
            wanted = {re.sub(r"[^a-z0-9]", "", keys[r].lower()) for r in q.get("expected", []) if r in keys}
            if re.sub(r"[^a-z0-9]", "", str(q.get("text")).lower()) not in wanted:
                problems.append(f"query {qid}: exactKey text must name the key of an expected item")
    seen.update(local)
    return problems


def queries_path_for(corpus_path):
    """corpus/<name>.json pairs with queries/<name>.json; <dir>/corpus.json with <dir>/queries.json."""
    if corpus_path.parent.name == "corpus":
        return corpus_path.parent.parent / "queries" / corpus_path.name
    return corpus_path.with_name("queries.json")


def main(argv):
    files = [Path(a) for a in argv] or sorted((HERE / "corpus").glob("*.json")) + [HERE / "sample" / "corpus.json"]
    seen, keys, query_ids, failed = set(), set(), set(), False
    for path in files:
        corpus = load(path)
        problems = validate_corpus(corpus, seen, keys)
        qpath = queries_path_for(path.resolve())
        if not problems and qpath.exists():
            problems += validate_queries(load(qpath), corpus, query_ids)
        for p in problems:
            print(f"{path.name}: {p}")
        failed |= bool(problems)
        if not problems:
            print(f"{path.name}: ok ({len(corpus['items'])} items)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
