#!/usr/bin/env python3
"""Eval harness against a running ShipLean (CA4).

  search-eval.py import --base-url URL --email E --password P [--sample] [--corpus-dir D] [--results-dir R]
  search-eval.py run    --base-url URL --label L [--prefix] [--email E --password P] [--queries-dir D] [--results-dir R]
"""
import argparse
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent / "search-eval"
sys.path.insert(0, str(HERE))

from harness.api import Api, ApiError  # noqa: E402
from harness.importer import corpus_files, import_all  # noqa: E402
from harness.runner import load_queries, run_queries  # noqa: E402


def parser():
    p = argparse.ArgumentParser(prog="search-eval.py")
    sub = p.add_subparsers(dest="command", required=True)
    imp = sub.add_parser("import", help="create the corpus's projects, sprints, items and comments")
    imp.add_argument("--base-url", required=True)
    imp.add_argument("--email", required=True)
    imp.add_argument("--password", required=True)
    imp.add_argument("--sample", action="store_true", help="import sample/corpus.json instead of corpus/*.json")
    imp.add_argument("--corpus-dir", type=Path)
    imp.add_argument("--results-dir", type=Path, default=HERE / "results")
    run = sub.add_parser("run", help="run every query through /api/search and write results/<label>.json")
    run.add_argument("--base-url", required=True)
    run.add_argument("--label", required=True)
    run.add_argument("--prefix", action="store_true")
    run.add_argument("--email")
    run.add_argument("--password")
    run.add_argument("--sample", action="store_true", help="use sample/queries.json instead of queries/*.json")
    run.add_argument("--queries-dir", type=Path)
    run.add_argument("--results-dir", type=Path, default=HERE / "results")
    return p


def main(argv=None):
    a = parser().parse_args(argv)
    api = Api(a.base_url)
    try:
        if a.command == "import":
            api.sign_in(a.email, a.password)
            files = [HERE / "sample" / "corpus.json"] if a.sample else corpus_files(a.corpus_dir or HERE / "corpus")
            started = time.perf_counter()
            refmap = import_all(api, files, a.results_dir)
            print(f"imported {len(refmap)} items in {time.perf_counter() - started:.1f}s; refmap in {a.results_dir / 'refmap.json'}")
        else:
            if a.email:
                api.sign_in(a.email, a.password)
            if a.sample:
                queries = json.loads((HERE / "sample" / "queries.json").read_text(encoding="utf-8"))
            else:
                queries = load_queries(a.queries_dir or HERE / "queries")
            refmap = json.loads((a.results_dir / "refmap.json").read_text(encoding="utf-8"))
            result = run_queries(api, queries, {k: r for r, k in refmap.items()}, a.label, a.prefix)
            out = a.results_dir / f"{a.label}.json"
            out.write_text(json.dumps(result, indent=1), encoding="utf-8")
            print(f"{len(queries)} queries -> {out}")
    except (ApiError, RuntimeError, OSError) as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
