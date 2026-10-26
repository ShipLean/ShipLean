# search-embeddings (spike)

Console app for the #35 search spike. Outside `src/` and `ShipLean.slnx`, so the repo build and `Directory.Build.props` ignore it.

```
dotnet run --project spikes/search-embeddings -- --ranker ilike --out scripts/search-eval/results/ilike.json
python scripts/search-eval/metrics.py scripts/search-eval/results/ilike.json
```

Flags: `--ranker` (required), `--out`, `--chunk-size`, `--model-dir`, `--inputs a.json,b.json`, `--cutoffs 0.45:0.80:0.05`,
and `--corpus` / `--queries` (a file or directory; default `scripts/search-eval/sample/`). `--queries` defaults to the paired file or sibling `queries/` directory. An unknown flag, a missing value or a bad number exits 2. A ranker implements `IRanker` and is registered by one
line in the dictionary at the top of `Program.cs`. Output is the CA2 rankings file: `{label, queries: {id: [[ref, score]]}, latencyMs}`.

Rankers: `ilike` (today's title/description ILIKE plus title `WordOverlap`, from `OrientReads.SearchAsync` without attachments).

The ILIKE baseline pools every project's items into one ranking (today's search runs inside one project), so it compares like with like against rankers that also span projects. It has no 500-row candidate limit.

`IRanker.Rank` returns exactly one `Rankings`. A cut-off sweep (`--cutoffs`) does not fit that, so the sweeping ranker prints its table from inside `Rank` and returns a placeholder ranking.

`lexical` mirrors production's lexical leg on a scratch database `search_spike_a3` (created and dropped by the run, on the `SHIPLEAN_TEST_DB` server): search_doc-like rows with the CB1 `tsv`, `websearch_to_tsquery` english || simple, `ts_rank_cd '{0.1,0.2,0.4,1.0}'`, a title+tags list and a body+comment list fused by RRF (k=60). `prefix`-kind queries run in prefix mode. `hybrid --inputs a.json,b.json` is RRF (k=60) over rankings files. `sweep --inputs scored.json --cutoffs 0.45:0.95:0.05` prints top-1/top-5/MRR/mean hits per cut-off, for all queries, paraphrase+bodyOnly and exactKey+titleWord. Exact-key lookup is not part of the tsv leg (production resolves keys elsewhere), so `lexical` scores 0 on exactKey.

A5 additions: `lexical-or` (the body/comment list ORs the words of `websearch_to_tsquery`; titles stay AND), `keyfirst --inputs ranked.json` (production's exact-key rule: a whole-query `^[A-Za-z]{2,4}-?\d{1,9}$` that names an item puts it first), `minilm256-mean` (MiniLM read to 256 tokens). `hybrid_cutoff.py lexical.json semantic.json from to step` evaluates the hybrid with the semantic leg cut at each cosine. Results and the decision are in `RESULTS.md`.
