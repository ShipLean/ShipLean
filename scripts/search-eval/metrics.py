#!/usr/bin/env python3
"""Print top-1, top-5 and MRR per query kind (CA2) for one or more rankings files."""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def score_query(expected, ranked_refs):
    """Returns (top1, top5, reciprocal rank) for one query; a missing ranking is a miss."""
    wanted = set(expected)
    rr = 0.0
    for i, ref in enumerate(ranked_refs):
        if ref in wanted:
            rr = 1.0 / (i + 1)
            break
    return (
        1.0 if ranked_refs[:1] and ranked_refs[0] in wanted else 0.0,
        1.0 if wanted & set(ranked_refs[:5]) else 0.0,
        rr,
    )


def score(queries, rankings):
    """{kind: (n, top1, top5, mrr)} plus an 'all' row."""
    by_kind = {}
    for q in queries:
        ranked = [entry[0] for entry in rankings["queries"].get(q["id"], [])]
        by_kind.setdefault(q["kind"], []).append(score_query(q["expected"], ranked))
        by_kind.setdefault("all", []).append(score_query(q["expected"], ranked))
    return {k: (len(v), *(sum(s[i] for s in v) / len(v) for i in range(3))) for k, v in by_kind.items()}


def table(queries, rankings_list):
    lines = ["| ranker | kind | n | top-1 | top-5 | MRR |", "|---|---|---|---|---|---|"]
    for rankings in rankings_list:
        scores = score(queries, rankings)
        for kind in sorted(k for k in scores if k != "all") + ["all"]:
            if kind in scores:
                n, t1, t5, mrr = scores[kind]
                lines.append(f"| {rankings['label']} | {kind} | {n} | {t1:.2f} | {t5:.2f} | {mrr:.2f} |")
    return "\n".join(lines)


def main(argv):
    args = list(argv)
    paths = [HERE / "queries", HERE / "sample" / "queries.json"]
    if "--queries" in args:
        i = args.index("--queries")
        paths = [Path(args[i + 1])]
        del args[i:i + 2]
    if not args:
        print("usage: metrics.py [--queries dir-or-file] rankings.json...", file=sys.stderr)
        return 2
    rankings_list = [load(a) for a in args]
    ranked_ids = {qid for r in rankings_list for qid in r["queries"]}
    queries, counted, skipped = [], [], []
    for entry in paths:
        for path in sorted(entry.glob("*.json")) if entry.is_dir() else [entry]:
            file_queries = load(path)
            # A queries file counts only when some ranking answered one of its ids; within a counted file a query
            # missing from a ranking is a miss. This lets a sample ranking be read without the other projects' queries.
            if any(q["id"] in ranked_ids for q in file_queries):
                queries += file_queries
                counted.append(path.name)
            else:
                skipped.append(path.name)
    print(table(queries, rankings_list))
    print(f"\nQuery files counted: {', '.join(counted) or 'none'}. Skipped (no ranking answered any of their queries): {', '.join(skipped) or 'none'}.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
