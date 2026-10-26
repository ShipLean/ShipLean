# Search embeddings spike: results (A5, ShipLean #35)

Full corpus (`scripts/search-eval/corpus`, 5 projects, 498 items, 82 queries; 1278 chunks at 1200 characters). Host: 2 intra-op threads, shared box, serialised runs. Numbers are top-5 hit rate per query kind unless a column says otherwise. Raw rankings are in `scripts/search-eval/results/`.

Small-sample warning: kinds have 10-14 queries each, so one query is 7-10 points. Differences of one query are not findings.

## Decision

| | |
|---|---|
| Model | multilingual-e5-small, the production int8 file (`model_qint8_avx512_vnni.onnx`), unchanged |
| Pooling | mean (CLS is 12 points worse overall, 21 on paraphrase, and prefix 0.60 vs 0.90) |
| Chunk size | 1200 characters |
| Cut-off | **0.83 cosine, applied to the semantic leg, for e5-small int8 + mean pooling only** |
| Retrieval | hybrid = RRF(k=60) of the AND lexical leg and the semantic leg, with the exact-key rule first |
| Embeddings-off fallback | lexical AND fused with lexical OR (not AND alone), exact-key first |

Reasons in short: hybrid with e5-int8-mean at 1200 gets 1.00 on paraphrase, bodyOnly and commentOnly and 0.99 overall once exact keys are resolved; int8 loses nothing to fp32 on quality and costs 40% less RSS and half the embed time; 800 vs 1200 vs 1600 is within one query (1600 loses one paraphrase), so the middle size wins on storage (1278 chunks vs 1432 at 800); MiniLM fails the issue's rule (below).

## Baseline and single legs (chunk 1200 unless the name says otherwise)

ilike is today's search. lexical is the production lexical leg (AND). lexical-or ORs the body/comment words (titles stay AND). Semantic rows are alone (no fusion), exact keys not resolved, so exactKey is ~0 for all of them. `@800`/`@1600` are semantic chunk sizes; hits/MRR as in metrics.py. Latency is per query (query embedding plus scan for semantic rankers; SQL for lexical).

| ranker | exactKey | titleWord | prefix | paraphrase | bodyOnly | commentOnly | nonEnglish | all top-5 | all top-1 | all MRR | para+body top-5 | p50/p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ilike | 0.00 | 1.00 | 0.60 | 0.57 | 0.21 | 0.00 | 0.69 | 0.44 | 0.30 | 0.37 | 0.39 | 1.4/4.7 |
| lexical | 0.00 | 1.00 | 1.00 | 0.07 | 0.64 | 0.91 | 0.23 | 0.52 | 0.46 | 0.49 | 0.36 | 3.1/4.2 |
| lexical-or | 0.00 | 1.00 | 1.00 | 0.50 | 0.93 | 0.36 | 0.69 | 0.65 | 0.55 | 0.60 | 0.71 | 6.4/63.1 |
| e5-int8-mean-800 | 0.00 | 1.00 | 0.90 | 1.00 | 0.93 | 1.00 | 0.92 | 0.84 | 0.73 | 0.78 | 0.96 | 4.6/8.4 |
| e5-int8-mean | 0.00 | 1.00 | 0.90 | 1.00 | 0.93 | 1.00 | 0.92 | 0.84 | 0.72 | 0.77 | 0.96 | 5.1/6.9 |
| e5-int8-mean-1600 | 0.00 | 1.00 | 0.90 | 0.93 | 0.93 | 1.00 | 0.92 | 0.83 | 0.73 | 0.77 | 0.93 | 4.6/7.9 |
| e5-fp32-mean-800 | 0.00 | 1.00 | 0.90 | 1.00 | 0.93 | 1.00 | 0.92 | 0.84 | 0.68 | 0.75 | 0.96 | 8.5/12.6 |
| e5-fp32-mean | 0.00 | 1.00 | 0.90 | 1.00 | 0.93 | 1.00 | 0.92 | 0.84 | 0.70 | 0.76 | 0.96 | 8.4/11.6 |
| e5-fp32-mean-1600 | 0.00 | 1.00 | 0.90 | 0.93 | 0.93 | 1.00 | 0.92 | 0.83 | 0.68 | 0.74 | 0.93 | 8.6/13.6 |
| e5-int8-cls | 0.00 | 1.00 | 0.60 | 0.79 | 0.79 | 0.91 | 0.85 | 0.72 | 0.62 | 0.68 | 0.79 | 5.1/6.9 |
| minilm-mean | 0.10 | 1.00 | 0.40 | 1.00 | 0.64 | 0.91 | 1.00 | 0.74 | 0.57 | 0.64 | 0.82 | 7.6/11.4 |
| minilm256-mean | 0.00 | 1.00 | 0.40 | 0.86 | 0.57 | 0.91 | 1.00 | 0.70 | 0.54 | 0.60 | 0.71 | 8.7/13.4 |

(The 800/1600 semantic files are `e5-*-mean-800.json` / `-1600.json`; the plain name is 1200. minilm256 is MiniLM-L12 read to 256 tokens instead of its default 128.)

## Hybrid = RRF(lexical, semantic), k=60

The lexical file is built at the same chunk size as the semantic one (`lexical-800.json` etc.). exactKey is 0 here because the tsv leg does not index keys; production resolves keys in the ranker.

| ranker | exactKey | titleWord | prefix | paraphrase | bodyOnly | commentOnly | nonEnglish | all top-5 | all top-1 | all MRR | para+body top-5 | p50/p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hybrid(lexical+e5-int8-mean) @800 | 0.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.87 | 0.77 | 0.81 | 1.00 | 7.7/12.4 |
| hybrid(lexical+e5-int8-mean) @1200 | 0.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.87 | 0.76 | 0.80 | 1.00 | 8.2/11.0 |
| hybrid(lexical+e5-int8-mean) @1600 | 0.00 | 1.00 | 1.00 | 0.93 | 1.00 | 1.00 | 0.92 | 0.85 | 0.76 | 0.80 | 0.96 | 7.6/12.9 |
| hybrid(lexical+e5-fp32-mean) @800 | 0.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.87 | 0.74 | 0.80 | 1.00 | 11.7/16.7 |
| hybrid(lexical+e5-fp32-mean) @1200 | 0.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.87 | 0.73 | 0.79 | 1.00 | 11.5/15.8 |
| hybrid(lexical+e5-fp32-mean) @1600 | 0.00 | 1.00 | 1.00 | 0.93 | 1.00 | 1.00 | 0.92 | 0.85 | 0.73 | 0.78 | 0.96 | 11.5/17.2 |
| hybrid(lexical+e5-int8-cls) @1200 | 0.00 | 1.00 | 1.00 | 0.79 | 1.00 | 1.00 | 0.85 | 0.82 | 0.73 | 0.77 | 0.89 | 8.5/10.7 |
| hybrid(lexical+minilm-mean) @1200 | 0.10 | 1.00 | 1.00 | 1.00 | 0.93 | 1.00 | 1.00 | 0.88 | 0.71 | 0.77 | 0.96 | 10.7/15.0 |
| hybrid(lexical+minilm256-mean) @1200 | 0.00 | 1.00 | 1.00 | 0.86 | 0.86 | 1.00 | 1.00 | 0.83 | 0.68 | 0.74 | 0.86 | 12.4/17.8 |

Latency for a hybrid row is the sum of its two legs' query latencies (fusion itself is negligible).

## Exact-key path (`keyfirst`)

`--ranker keyfirst --inputs x.json` models production's rule: if the whole query matches `^[A-Za-z]{2,4}-?\d{1,9}$` and `PROJECTKEY-n` names an item in the corpus, that item goes first and the rest follows. It touches only key-shaped queries, so every other column equals the unwrapped row. exactKey goes 0 to 1.00 (10 queries) and overall top-1 rises about 12 points.

| ranker | exactKey | titleWord | prefix | paraphrase | bodyOnly | commentOnly | nonEnglish | all top-5 | all top-1 | all MRR | para+body top-5 | p50/p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| keyfirst(hybrid(lexical+e5-int8-mean)) @800 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.99 | 0.89 | 0.94 | 1.00 | 7.7/12.4 |
| keyfirst(hybrid(lexical+e5-int8-mean)) @1200 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.99 | 0.88 | 0.93 | 1.00 | 8.2/11.0 |
| keyfirst(hybrid(lexical+e5-int8-mean)) @1600 | 1.00 | 1.00 | 1.00 | 0.93 | 1.00 | 1.00 | 0.92 | 0.98 | 0.88 | 0.92 | 0.96 | 7.6/12.9 |
| keyfirst(hybrid(lexical+e5-fp32-mean)) @1200 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 | 0.92 | 0.99 | 0.85 | 0.91 | 1.00 | 11.5/15.8 |
| keyfirst(hybrid(lexical+e5-int8-cls)) @1200 | 1.00 | 1.00 | 1.00 | 0.79 | 1.00 | 1.00 | 0.85 | 0.94 | 0.85 | 0.89 | 0.89 | 8.5/10.7 |
| keyfirst(hybrid(lexical+minilm-mean)) @1200 | 1.00 | 1.00 | 1.00 | 1.00 | 0.93 | 1.00 | 1.00 | 0.99 | 0.83 | 0.89 | 0.96 | 10.7/15.0 |
| keyfirst-ilike | 1.00 | 1.00 | 0.60 | 0.57 | 0.21 | 0.00 | 0.69 | 0.56 | 0.43 | 0.49 | 0.39 | 1.4/4.7 |
| keyfirst-lexical | 1.00 | 1.00 | 1.00 | 0.07 | 0.64 | 0.91 | 0.23 | 0.65 | 0.59 | 0.62 | 0.36 | 3.1/4.2 |
| keyfirst-lexical-or | 1.00 | 1.00 | 1.00 | 0.50 | 0.93 | 0.36 | 0.69 | 0.77 | 0.67 | 0.72 | 0.71 | 6.4/63.1 |

## OR-ed lexical (A3 finding 1)

`lexical-or`: the body+comment list uses `replace(websearch_to_tsquery(..)::text, ' & ', ' | ')::tsquery` for both the english and simple queries; titles+tags stay AND; ranking is still ts_rank_cd, RRF k=60 between the two lists. Alone vs ILIKE and vs lexical AND:

| kind | ilike | lexical (AND) | lexical-or | lexical AND+OR (RRF) |
|---|---|---|---|---|
| paraphrase | 0.57 | 0.07 | 0.50 | 0.50 |
| nonEnglish | 0.69 | 0.23 | 0.69 | 0.69 |
| bodyOnly | 0.21 | 0.64 | 0.93 | 1.00 |
| commentOnly | 0.00 | 0.91 | 0.36 | 0.91 |
| prefix | 0.60 | 1.00 | 1.00 | 1.00 |
| titleWord | 1.00 | 1.00 | 1.00 | 1.00 |
| all top-5 | 0.44 | 0.52 | 0.65 | 0.73 |

OR fixes nonEnglish (back to ILIKE's level) and most of paraphrase, but it returns about 51 hits per query (AND: 3.4) and buries comment hits (commentOnly 0.91 to 0.36) because many comments share one common word. Fusing the AND and OR lists keeps AND's precision on comments and OR's recall, and is the best no-embeddings option. Cost: OR p95 63 ms on 498 items (AND 4 ms); the GIN scan on a larger corpus will be worse, so B6b should cap the OR list (top 100 already is) and time it.

OR does not help the hybrid: with embeddings on, hybrid(lexical-or+semantic) is worse than hybrid(lexical+semantic) by 8 points overall (the 50-item OR list outvotes the semantic list in RRF):

| ranker | exactKey | titleWord | prefix | paraphrase | bodyOnly | commentOnly | nonEnglish | all top-5 | all top-1 | all MRR | para+body top-5 | p50/p95 ms |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| hybrid(lexical-or+e5-int8-mean) @1200 | 0.00 | 1.00 | 1.00 | 0.79 | 0.93 | 0.82 | 0.92 | 0.79 | 0.65 | 0.71 | 0.86 | 11.6/69.2 |
| hybrid(lexical-or+e5-fp32-mean) @1200 | 0.00 | 1.00 | 1.00 | 0.79 | 0.93 | 0.82 | 0.92 | 0.79 | 0.62 | 0.70 | 0.86 | 14.9/73.7 |
| hybrid(lexical-or+minilm-mean) @1200 | 0.10 | 1.00 | 1.00 | 0.79 | 0.93 | 0.73 | 0.92 | 0.79 | 0.62 | 0.70 | 0.86 | 14.0/74.2 |
| hybrid-lexical-lexical-or | 0.00 | 1.00 | 1.00 | 0.50 | 1.00 | 0.91 | 0.69 | 0.73 | 0.62 | 0.68 | 0.75 | 9.7/66.4 |

So: **embeddings on = AND lexical + semantic; embeddings off = AND lexical fused with OR lexical.** The off fallback still loses one paraphrase query (0.50 vs ILIKE 0.57, one query of 14) and wins everywhere else (all: 0.73 vs 0.44; with exact-key first 0.85 vs 0.56 for ILIKE with the same rule).

## Cut-off sweep (mean pooling, int8)

Semantic leg alone, step 0.05 then 0.01 around the knee (`--ranker sweep`). "Noise" is exactKey+titleWord, where the answer is lexical and every extra semantic hit is noise; hits are mean results per query, capped at 50 by the rankings file, so hits at 0.80 and below are lower bounds.

e5-int8-mean, 0.45-0.95 step 0.05:

| cut-off | all top-5 | all hits | para+body top-5 | para+body hits | key+titleWord top-5 | key+titleWord hits |
|---|---|---|---|---|---|---|
| 0.45 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.50 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.55 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.60 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.65 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.70 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.75 | 0.84 | 50.0 | 0.96 | 50.0 | 0.50 | 50.0 |
| 0.80 | 0.84 | 45.5 | 0.96 | 47.1 | 0.50 | 44.2 |
| 0.85 | 0.63 | 2.2 | 0.82 | 3.5 | 0.45 | 0.9 |
| 0.90 | 0.15 | 0.1 | 0.11 | 0.1 | 0.00 | 0.0 |
| 0.95 | 0.00 | 0.0 | 0.00 | 0.0 | 0.00 | 0.0 |


e5-int8-mean, 0.80-0.87 step 0.01:

| cut-off | all top-5 | all hits | para+body top-5 | para+body hits | key+titleWord top-5 | key+titleWord hits |
|---|---|---|---|---|---|---|
| 0.80 | 0.84 | 45.5 | 0.96 | 47.1 | 0.50 | 44.2 |
| 0.81 | 0.84 | 33.3 | 0.96 | 43.9 | 0.50 | 23.6 |
| 0.82 | 0.80 | 22.8 | 0.93 | 35.8 | 0.50 | 9.8 |
| 0.83 | 0.80 | 12.6 | 0.93 | 20.2 | 0.50 | 3.6 |
| 0.84 | 0.72 | 5.3 | 0.86 | 8.8 | 0.45 | 1.8 |
| 0.85 | 0.63 | 2.2 | 0.82 | 3.5 | 0.45 | 0.9 |
| 0.86 | 0.55 | 1.0 | 0.71 | 1.5 | 0.25 | 0.5 |
| 0.87 | 0.44 | 0.6 | 0.54 | 0.8 | 0.20 | 0.3 |

Same for e5-fp32-mean (fp32 knee is the same): top-5 on paraphrase+bodyOnly 0.96 until 0.81-0.82, 0.93 at 0.83, 0.86 at 0.84; noise hits 22.5 / 9.5 / 3.5 / 1.9 at 0.81 / 0.82 / 0.83 / 0.84.

Mean-pooled e5 cosines are high: relevant and irrelevant hits mostly sit between 0.75 and 0.85, so the old default 0.55 filters nothing (50 hits per query up to 0.75), and Supply's 0.90 (CLS) would return nothing (0.15 top-5 at 0.90 mean).

### Cut-off on the hybrid (what ships)

The cut-off is applied to the semantic leg before RRF (`spikes/search-embeddings/hybrid_cutoff.py lexical.json semantic.json from to step`). Cells are top-5 / mean hits.

lexical
| cut-off | all top-5 / hits | para+body top-5 / hits | exactKey+titleWord top-5 / hits | commentOnly top-5 / hits |
|---|---|---|---|---|
| 0.80 | 0.87 / 47.4 | 1.00 / 47.2 | 0.50 / 45.3 | 1.00 / 46.8 |
| 0.81 | 0.87 / 35.3 | 1.00 / 43.9 | 0.50 / 24.8 | 1.00 / 39.6 |
| 0.82 | 0.87 / 25.1 | 1.00 / 35.8 | 0.50 / 11.3 | 1.00 / 32.2 |
| 0.83 | 0.87 / 15.2 | 1.00 / 20.3 | 0.50 / 5.7 | 1.00 / 17.6 |
| 0.84 | 0.84 / 8.1 | 0.96 / 8.9 | 0.50 / 4.2 | 1.00 / 5.3 |
| 0.85 | 0.83 / 5.1 | 0.93 / 3.6 | 0.50 / 3.8 | 1.00 / 1.7 |

(The same script on `lexical-or` shows the OR list drowns the cut-off effect, 80+ hits at every cut-off.)

**Rule used:** the highest cut-off at which the hybrid keeps paraphrase+bodyOnly top-5 at its no-cut-off value (1.00), because every step up removes noise (exactKey+titleWord hits) and the next step down in the table costs a query. That is **0.83** (para+body 1.00, exactKey+titleWord hits 5.7 down from 51+, commentOnly 1.00); 0.84 drops one paraphrase/bodyOnly query (0.96). On the semantic leg alone the same rule gives 0.81 (0.82 and 0.83 each lose one query of 28), which is why the number is read off the hybrid: that is the ranker that ships, and the lexical leg covers the semantic queries that sit just under 0.83. The margin is one 0.01 step; B6b must confirm 0.83 against the real ranker and keep the sweep as the regression check, because the curve is steep (hits halve every step between 0.81 and 0.84).

CLS for contrast (same rule, e5-int8-cls): hybrid keeps para+body at its 0.89 (itself 11 points below mean) up to 0.92, then collapses (0.68 at 0.93); semantic-alone knee 0.91-0.92 (34.3 hits at 0.90, 13.2 at 0.91, 2.8 at 0.92). The CLS cut-off would be 0.92, and it is not for sale: mean-pooled and CLS cosines are different scales (0.83 vs 0.92 here), which is the amendment's point.

## Model decision rule

The issue: e5-small stays unless MiniLM-L12 is at least 5 points better top-5 on paraphrase+bodyOnly, with no language worse.

| | e5-int8-mean | minilm-mean (128 tokens) | minilm256-mean |
|---|---|---|---|
| semantic alone, para+body top-5 | 0.96 | 0.82 | 0.71 |
| semantic alone, nonEnglish | 0.92 | 1.00 | 1.00 |
| hybrid, para+body top-5 | 1.00 | 0.96 | 0.86 |
| hybrid, nonEnglish | 0.92 | 1.00 | 1.00 |
| all top-5 (semantic alone) | 0.84 | 0.74 | 0.70 |
| prefix (semantic alone) | 0.90 | 0.40 | 0.40 |

MiniLM is 14 points worse on para+body (bodyOnly 0.64 vs 0.93), so the first condition fails by a wide margin; it is better on nonEnglish by one query, which does not matter. Reading it to 256 tokens does not help (worse, not better). **e5-small stays.**

## Latency and memory

Per-query latency is p50/p95 over 82 queries, from the tables above (query embedding plus brute-force scan of 1278 vectors, 2 threads). RSS is `/usr/bin/time -v` Maximum resident set size for the whole spike run (loads the model, embeds 1278 chunks in batches of 16, then answers 82 queries), so it includes the batch embedding peak, not only query-time steady state.

| model | per-query p50 / p95 ms | embed 1278 chunks | peak RSS (chunk 1200) |
|---|---|---|---|
| e5-int8 (mean or CLS) | 5.1 / 6.9 | 19.0 s | 602 MB (800: 465 MB, 1600: 644 MB) |
| e5-fp32 mean | 8.4 / 11.6 | 38.6 s | 985 MB (800: 978 MB, 1600: 1046 MB) |
| minilm-L12 mean (128 tok) | 7.6 / 11.4 | 26.8 s | 979 MB |
| minilm-L12 mean (256 tok) | 8.7 / 13.4 | 38.3 s | 979 MB |
| lexical AND | 3.1 / 4.2 | | |
| lexical OR | 6.4 / 63.1 | | |
| ilike | 1.4 / 4.7 | | |
| hybrid(lexical+e5-int8-mean) | 8.2 / 11.0 | | |

Chunks per corpus: 1432 at 800, 1278 at 1200, 1260 at 1600. The shipped model is the VNNI-tuned int8 file running on a CPU without VNNI here; it did not lose quality to fp32, and A2's drift note (about 5e-3 batch vs single) did not show in the rankings.

## What B6b and the conductor need

- SHIPLEAN_SEARCH_CUTOFF default 0.83 for e5-small int8, mean pooling, chunk 1200. It belongs to that model + pooling pair; changing the model, quantisation, pooling or chunk size means rerunning `sweep` and `hybrid_cutoff.py`. Confirm against the real ranker.
- Apply the cut-off to the semantic leg before fusion, not to the fused list (RRF scores are not cosines).
- Keep the lexical leg AND (production's current). With embeddings off, add the OR body list and fuse it with the AND list as above; this is the only fallback measured that does not regress paraphrase/nonEnglish by more than one query against ILIKE.
- The exact-key rule is worth about 12 points of top-1; it must run in the ranker for both modes.
- Caveats: one author wrote this corpus and the queries; 10-14 queries per kind; exactKey, titleWord and prefix are easy for lexical so the semantic-vs-lexical gap on real traffic may differ; hits-per-query are capped at 50.

## Reproducing

```
dotnet run -c Release --project spikes/search-embeddings -- --corpus scripts/search-eval/corpus --ranker <name> --chunk-size <n> --out scripts/search-eval/results/<file>.json
```
Rankers added by A5: `lexical-ortitle`, `titles-and`, `titles-or`, `lexical-or`, `keyfirst`, `minilm256-mean` (plus A2/A3's). Hybrid: `--ranker hybrid --inputs lexical.json,e5-int8-mean.json`; then `--ranker keyfirst --inputs hybrid.json`.

## Draft comment for issue #35

> Spike result (full corpus, 498 items, 82 queries). Staying on multilingual-e5-small, the int8 file we already ship, mean pooling, 1200-character chunks. MiniLM-L12 fails the rule: 14 points worse top-5 on paraphrase+bodyOnly (0.82 vs 0.96), better only on nonEnglish by one query. int8 matches fp32 quality at 602 MB vs 985 MB RSS and half the embed time. Chunk size 800/1200/1600 is within one query; 1200 gives fewer chunks.
> Hybrid (RRF of AND lexical + semantic) with exact keys resolved first: 0.99 top-5 overall, 1.00 on paraphrase, bodyOnly and commentOnly, against 0.44 for today's ILIKE. Mean-pooled cosines run 0.75-0.85, so the old 0.55 default filters nothing and Supply's 0.90 would return nothing. Measured on the hybrid, the cut-off is 0.83 for this model + pooling (highest value that keeps paraphrase+bodyOnly top-5 at 1.00; noise hits on exactKey+titleWord 51 down to 5.7); 0.84 loses a query. It is only valid for e5-small int8 mean; changing either means re-measuring.
> With embeddings off, AND-only lexical is worse than ILIKE on paraphrase (0.07 vs 0.57) and nonEnglish (0.23 vs 0.69). Fusing it with an OR-ed body list gives 0.50 / 0.69 (ILIKE level) and 0.73 overall vs 0.44, at p95 about 65 ms on this corpus. Per-query latency for the hybrid is 8 / 11 ms p50 / p95.
> Caveats: small hand-written corpus, 10-14 queries per kind, one-query cliffs around the cut-off. Details in spikes/search-embeddings/RESULTS.md.

Not committed (regenerate with the commands above): the hybrid(lexical-or+semantic) files and the keyfirst wrappers of minilm256 and fp32 at 800/1600.

## Addendum: OR on titles (`lexical-ortitle`)

Asked by the conductor: does ORing the title+tags list (partial-word title matches, as ILIKE plus word overlap returns today) cost anything in the embeddings-on hybrid? `lexical-ortitle` = title+tags list OR, body+comment list AND. `titles-and` / `titles-or` are the title+tags list alone. Cells are top-1 / top-5; hits are per query. Hybrids are RRF(k=60) with the e5-int8-mean @1200 semantic leg, with or without the 0.83 semantic cut-off (`hybrid_cutoff.py ... out.json`), and with `keyfirst`.

| ranker | exactKey | titleWord | prefix | paraphrase | bodyOnly | commentOnly | nonEnglish | all | hits/q exactKey+titleWord | hits/q all |
|---|---|---|---|---|---|---|---|---|---|---|
| lexical | 0.00/0.00 | 0.90/1.00 | 0.70/1.00 | 0.07/0.07 | 0.57/0.64 | 0.91/0.91 | 0.23/0.23 | 0.46/0.52 | 3.7 | 3.4 |
| lexical-ortitle | 0.00/0.00 | 0.90/1.00 | 0.70/1.00 | 0.36/0.57 | 0.43/0.71 | 0.36/0.91 | 0.62/0.69 | 0.48/0.70 | 3.7 | 37.8 |
| titles-and | 0.00/0.00 | 0.80/1.00 | 0.50/1.00 | 0.07/0.07 | 0.00/0.00 | 0.00/0.00 | 0.15/0.15 | 0.20/0.28 | 1.6 | 1.0 |
| titles-or | 0.00/0.00 | 0.80/1.00 | 0.50/1.00 | 0.36/0.57 | 0.00/0.07 | 0.00/0.00 | 0.62/0.69 | 0.32/0.46 | 1.6 | 35.5 |
| hybrid-lexical-nocut | 0.00/0.00 | 0.80/1.00 | 0.90/1.00 | 0.79/1.00 | 0.86/1.00 | 1.00/1.00 | 0.85/0.92 | 0.76/0.87 | 51.0 | 51.8 |
| hybrid-lexical-ortitle-nocut | 0.00/0.00 | 0.80/1.00 | 0.90/1.00 | 0.57/0.79 | 0.57/0.86 | 0.73/1.00 | 0.77/0.92 | 0.62/0.80 | 51.0 | 78.7 |
| hybrid-lexical-cut083 | 0.00/0.00 | 0.80/1.00 | 0.90/1.00 | 0.79/1.00 | 0.86/1.00 | 1.00/1.00 | 0.85/0.92 | 0.76/0.87 | 5.7 | 15.2 |
| hybrid-lexical-ortitle-cut083 | 0.00/0.00 | 0.80/1.00 | 0.90/1.00 | 0.57/0.79 | 0.57/0.93 | 0.82/1.00 | 0.77/0.92 | 0.63/0.82 | 5.7 | 45.8 |
| keyfirst-hybrid-lexical-nocut | 1.00/1.00 | 0.80/1.00 | 0.90/1.00 | 0.79/1.00 | 0.86/1.00 | 1.00/1.00 | 0.85/0.92 | 0.88/0.99 | 51.5 | 51.9 |
| keyfirst-hybrid-lexical-ortitle-nocut | 1.00/1.00 | 0.80/1.00 | 0.90/1.00 | 0.57/0.79 | 0.57/0.86 | 0.73/1.00 | 0.77/0.92 | 0.74/0.93 | 51.5 | 78.8 |
| keyfirst-hybrid-lexical-cut083 | 1.00/1.00 | 0.80/1.00 | 0.90/1.00 | 0.79/1.00 | 0.86/1.00 | 1.00/1.00 | 0.85/0.92 | 0.88/0.99 | 6.2 | 15.4 |
| keyfirst-hybrid-lexical-ortitle-cut083 | 1.00/1.00 | 0.80/1.00 | 0.90/1.00 | 0.57/0.79 | 0.57/0.93 | 0.82/1.00 | 0.77/0.92 | 0.76/0.94 | 6.2 | 45.9 |

Findings:
- Title list alone: AND returns 1.0 hit per query, OR 35.5. OR lifts nonEnglish (0.15 to 0.69) and paraphrase (0.07 to 0.57) on titles but adds nothing to titleWord/exactKey/prefix (single-word queries behave the same under AND and OR; hits on exactKey+titleWord are identical: 3.7 in the lexical file, 5.7 / 6.2 in the cut hybrids).
- Lexical alone (title OR, body AND): top-5 0.52 to 0.70, but commentOnly top-1 0.91 to 0.36 (top-5 stays 0.91) and bodyOnly top-1 0.57 to 0.43; 37.8 hits per query against 3.4.
- **In the hybrid it costs.** At the 0.83 cut-off with keyfirst: top-5 0.99 to 0.94, top-1 0.88 to 0.76, paraphrase top-5 1.00 to 0.79 (top-1 0.79 to 0.57), bodyOnly top-5 1.00 to 0.93, commentOnly top-1 1.00 to 0.82. Hits per query 15.4 to 45.9. Without the cut-off it is worse still (0.93 top-5, 78.8 hits). The 35-hit OR title list is a full RRF voter and its loosely matching titles outrank semantically right items.
- Recommendation: keep AND titles in the embeddings-on hybrid. If the MCP test needs a partial-word title match, keep it as a separate low-weight or fallback path (embeddings off, or when the AND title list is empty), not a voter in the hybrid RRF. Not measured: that low-weight variant.
