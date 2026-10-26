"""Queries -> /api/search -> a rankings file (CA2). Keys in the answer map back to refs through refmap.json."""
import json
import time
from pathlib import Path

from .api import ApiError


def run_queries(api, queries, key_to_ref, label, prefix=False, limit=10):
    rankings, latency = {}, {}
    for q in queries:
        params = {"q": q["text"], "limit": limit, "closed": "true"}
        if prefix:
            params["prefix"] = "true"
        started = time.perf_counter()
        answer = api.send("GET", "/api/search", params=params)
        latency[q["id"]] = (time.perf_counter() - started) * 1000.0
        ranked = []
        for item in answer["items"]:
            if item["key"] not in key_to_ref:
                raise ApiError(f"{q['id']}: result {item['key']} is not in the refmap; was the corpus imported into this instance?")
            ranked.append([key_to_ref[item["key"]], None])
        rankings[q["id"]] = ranked
    return {"label": label, "queries": rankings, "latencyMs": latency}


def load_queries(queries_dir):
    queries = []
    for path in sorted(Path(queries_dir).glob("*.json")):
        queries.extend(json.loads(path.read_text(encoding="utf-8")))
    return queries
