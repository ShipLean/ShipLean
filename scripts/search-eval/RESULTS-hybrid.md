# Hybrid search: full-corpus eval against the real ranker (B6b, ShipLean #35)

Real app (Release build, `nice -n 10`), real model (e5-small int8, mean pooling), fresh database, the full corpus imported over REST (498 items, 5 projects, 82 queries), `POST /api/admin/search/reindex` until nothing was pending (1297 rows embedded). `prefix` queries ran with `prefix=true`, the rest without; every query with `closed=true`, limit 10. Host: shared, load average 9-17 from CI, so latencies are pessimistic. Small-sample warning as in the spike: one query is 7-10 points on a kind.

## Final table (cut-off 0.84, the default; boosts unchanged)

Three rankers over the same ranker code:

- **lexical-only**: the AND lexical leg alone (`SHIPLEAN_SEARCH_CUTOFF=1.0`, so the semantic leg lists nothing). This is "lexical" in the ship bar: the leg the hybrid adds a semantic leg to.
- **hybrid**: AND lexical + semantic, RRF k=60, exact key first, boosts.
- **embeddings off**: `SHIPLEAN_EMBEDDINGS=off`, the fallback: AND lexical lists fused with the any-word body list.

| kind | n | lexical-only top-1 / top-5 / MRR | hybrid top-1 / top-5 / MRR | embeddings off top-1 / top-5 / MRR |
|---|---|---|---|---|
| exactKey | 10 | 1.00 / 1.00 / 1.00 | 1.00 / 1.00 / 1.00 | 1.00 / 1.00 / 1.00 |
| titleWord | 10 | 1.00 / 1.00 / 1.00 | 0.90 / 1.00 / 0.95 | 1.00 / 1.00 / 1.00 |
| prefix | 10 | 0.70 / 1.00 / 0.85 | 0.90 / 1.00 / 0.95 | 0.70 / 1.00 / 0.85 |
| paraphrase | 14 | 0.07 / 0.07 / 0.07 | 0.64 / 0.93 / 0.75 | 0.43 / 0.50 / 0.50 |
| bodyOnly | 14 | 0.64 / 0.64 / 0.64 | 0.86 / 1.00 / 0.91 | 0.79 / 0.93 / 0.84 |
| commentOnly | 11 | 0.91 / 0.91 / 0.91 | 0.82 / 1.00 / 0.91 | 0.91 / 0.91 / 0.92 |
| nonEnglish | 13 | 0.23 / 0.23 / 0.23 | 0.62 / 0.85 / 0.73 | 0.38 / 0.69 / 0.49 |
| **all** | 82 | 0.61 / 0.65 / 0.63 | **0.80 / 0.96 / 0.87** | 0.72 / 0.84 / 0.78 |

Latency (client round trip over loopback, so an upper bound on server time; the app's request log does not record `/api/search`, so there is no server-side figure). The first query after a start loads the vector cache (about 1 s); the slow max below is that one.

| ranker | p50 ms | p95 ms (runs) |
|---|---|---|
| hybrid (0.84) | 53-62 | 69, 82 (the 82 run is the first after a start, with the cold cache) |
| embeddings off (AND + any-word) | 37-41 | 69, 79 |
| embeddings off, 30 short-prefix two-word queries ("login re", 2-letter prefix) | 25-26 | 36, 40 |
| lexical-only | 61 | 89 (cold cache, semantic leg on) |

Both p95 numbers (hybrid and embeddings off) are under 150 ms. The short-prefix queries run without an any-word prefix term (a prefix under 3 characters is left out of that list), so they cost less than the full-word queries. The lexical-only and sweep rows below were measured at 0.85 and earlier on an earlier build; the 0.84 hybrid and embeddings-off rows are re-run on the final build (commit 60a061a plus the short-prefix guard).

## Ship bar

- hybrid top-5 minus lexical-only top-5: paraphrase 0.93 - 0.07 = **+86 points**, bodyOnly 1.00 - 0.64 = **+36 points**. Met (bar: +20).
- exactKey top-5 1.00 -> 1.00, titleWord top-5 1.00 -> 1.00: no drop. titleWord top-1 is 0.90 against 1.00: one query, "voucher", where the open Epic "Voucher codes" (shop-068) ranks above the two closed stories the corpus names (shop-069/070). Both are in the top 3. The open boost is not the cause (Open 1.05 gave the same result); the semantic lists rank the Epic first. Boosts left as they are.
- p95 < 150 ms: hybrid and the fallback both met.
- Against the **embeddings-off** ranker the +20 on bodyOnly cannot hold (0.93 vs 1.00, +7): the any-word list that A5 added to the fallback lifts the lexical baseline from 0.64 to 0.93 on body-only text, so a gap of 20 points is not available. The bar was written against the AND lexical leg and is met against it; the fallback is a separate ranker that is better than the bar's baseline. Paraphrase is +43 against it (0.93 vs 0.50).

## Cut-off against the real ranker (hybrid, boosts unchanged)

A5 measured 0.83 on the semantic leg alone. In the real ranker the semantic lists share RRF with the lexical ones, so a low cut-off floods the fusion with weak hits.

| cut-off | all top-5 | all top-1 | paraphrase top-5 | bodyOnly top-5 | nonEnglish top-5 | titleWord top-1 |
|---|---|---|---|---|---|---|
| 0.80 | 0.91 | 0.68 | 0.86 | 0.79 | 0.92 | 1.00 |
| 0.82 | 0.93 | 0.68 | 0.79 | 0.86 | 0.92 | 0.90 |
| 0.83 | 0.95 | 0.77 | 0.86 | 0.93 | 0.92 | 0.90 |
| **0.84** (default; first sweep) | 0.95 | 0.79 | 0.86 | 1.00 | 0.85 | 0.90 |
| 0.85 | 0.96 | 0.85 | 0.93 | 1.00 | 0.85 | 0.90 |
| 0.86 | 0.93 | 0.84 | 0.71 | 1.00 | 0.85 | 0.90 |
| 0.87 | 0.87 | 0.79 | 0.36 | 1.00 | 0.85 | 0.90 |
| 0.88 | 0.84 | 0.77 | 0.36 | 1.00 | 0.69 | 0.90 |

The sweep rows are from the first pass at each value (one run each; the 0.84 final-build re-run reads all top-5 0.96, paraphrase 0.93, and shows the run-to-run noise of a few points on small kinds). 0.85 scores best, but it is the top of a plateau and sits next to a cliff, so the default is 0.84, a full step of margin below it: paraphrase queries are long, and their best cosines are around 0.85-0.86. Real queries are shorter, so their cosines may sit differently; 0.84 gives the same all-top-5 (0.95-0.96). Valid for e5-small int8 with mean pooling only. A nonEnglish query is lost between 0.83 and 0.85 (0.92 -> 0.85).

## Embeddings-off fallback

The item lexical leg, when it gets no query vector, adds `BodyLexicalAny`: the body, comment and attachment rows matched with the words joined by `or`, read with the `english` config only. Variants measured (embeddings off, all top-5 / commentOnly top-5 / p95):

| fallback | all | commentOnly | paraphrase | p95 ms |
|---|---|---|---|---|
| AND only (no any-word list) | 0.65 | 0.91 | 0.07 | 63 |
| any-word title and body lists, english + simple | 0.82 | 0.82 | 0.43 | 128-142 |
| any-word title and body lists, english only | 0.80 | 0.64 | 0.50 | 84-101 |
| **any-word body list only, english only (shipped)** | **0.84** | **0.91** | 0.50 | 57-73 |

The simple config keeps stop words ("the", "to", "in"), so an OR over them matches nearly every row and ranking them costs 80 ms by itself; the english config drops them. An any-word title list adds noise (commentOnly 0.91 -> 0.64) for no top-5 gain.

## Reproduce

All commands from the repo root of the worktree, after `source ~/.local/shiplean-env.sh` (Python 3.12 with `requests`; `scripts/search-eval.py` imports `scripts/search-eval/harness`). Results go to a scratch folder, `R=/tmp/search-eval-results` (the default `scripts/search-eval/results` is gitignored too).

**Database and app.** `psql -U postgres -c 'create database search_b6b'` against the test Postgres (without psql on the host: `docker exec <postgres-container> psql …`) (empty; the app migrates itself). Build `shiplean-build`, then run the Release output under `nice`:

```sh
cd src/ShipLean.WebApi/bin/Release/net10.0
SHIPLEAN_DB_CONNECTION="Host=127.0.0.1;Port=55432;Username=postgres;Password=<from SHIPLEAN_TEST_DB>;Database=search_b6b" \
SHIPLEAN_ADMIN_EMAIL=eval@example.test SHIPLEAN_ADMIN_PASSWORD='eval-Pass-12345' \
SHIPLEAN_BLOB_ROOT=/tmp/search-eval-blobs ASPNETCORE_URLS=http://127.0.0.1:5391 \
SHIPLEAN_EMBEDDINGS=on SHIPLEAN_EMBEDDINGS_MODEL_PATH=$HOME/.cache/shiplean/models/multilingual-e5-small \
  nice -n 10 dotnet ShipLean.WebApi.dll
```

Per ranker, change only these variables (restart the app between rankers; the data and vectors stay in the database):

| ranker | env |
|---|---|
| hybrid (default) | none extra (cut-off 0.84); sweep rows: `SHIPLEAN_SEARCH_CUTOFF=0.80` ... `0.88` |
| lexical-only (AND lexical leg alone) | `SHIPLEAN_EMBEDDINGS=on SHIPLEAN_SEARCH_CUTOFF=1.0`: the query is embedded, so the lexical leg stays AND, but no chunk reaches a cosine of 1.0, so the semantic lists are empty |
| embeddings off | `SHIPLEAN_EMBEDDINGS=off` (model path unused) |

**Import and reindex** (once, hybrid app running; the import takes 35-60 s for 498 items):

```sh
R=/tmp/search-eval-results; mkdir -p $R
python3.12 scripts/search-eval.py import --base-url http://127.0.0.1:5391 --email eval@example.test --password 'eval-Pass-12345' --results-dir $R
curl -s -c $R/cj -H 'content-type: application/json' -H 'X-ShipLean-Client: x' -d '{"email":"eval@example.test","password":"eval-Pass-12345"}' http://127.0.0.1:5391/api/auth/login -o /dev/null
curl -s -b $R/cj -X POST -H 'X-ShipLean-Client: x' "http://127.0.0.1:5391/api/admin/search/reindex?full=true" --max-time 900
```

A pass stops after about 77 s with `"remaining":32`; call `.../reindex?full=false` again until the answer is `"remaining":0` (the background pass may also finish the rest; check `select count(*) from search_doc where embedding is null and field in ('title','body','comment','sprint_goal')` is 0). 1297 rows end up embedded (sprint names, project and tags rows are lexical only). The embeddings-off and cut-off runs reuse these vectors.

**Run.** `--prefix` is global in `harness/runner.py` (one flag for every query in the run), so the corpus is run twice and merged: the plain run answers every kind except `prefix`, the `--prefix` run answers the `prefix` kind. Each run uses `closed=true`, limit 10 (hard-coded in the runner), the default `scripts/search-eval/queries`:

```sh
for m in plain prefix; do
  python3.12 scripts/search-eval.py run --base-url http://127.0.0.1:5391 --email eval@example.test --password 'eval-Pass-12345' \
    --label <label>-$m $( [ $m = prefix ] && echo --prefix ) --results-dir $R
done
```

**Merge, score and p95** (what the scratch `merge.py` did; run from `scripts/search-eval`):

```sh
python3 - <<'PY'
import glob, json, statistics, sys
import metrics
R, label = "/tmp/search-eval-results/", "<label>"
plain, pre = (json.load(open(f"{R}{label}-{m}.json")) for m in ("plain", "prefix"))
qs = [q for f in sorted(glob.glob("queries/*.json")) for q in json.load(open(f))]
out = {"label": label, "queries": {}, "latencyMs": {}}
for q in qs:  # the prefix kind from the --prefix run, every other kind from the plain run
    src = pre if q["kind"] == "prefix" else plain
    out["queries"][q["id"]], out["latencyMs"][q["id"]] = src["queries"][q["id"]], src["latencyMs"][q["id"]]
lat = sorted(out["latencyMs"].values())      # 82 client round trips in ms, one per query
print("p50 %.1f p95 %.1f max %.1f" % (statistics.median(lat), lat[int(len(lat) * 0.95) - 1], lat[-1]))  # p95 = the 77th of 82 sorted values
for kind, (n, t1, t5, mrr) in sorted(metrics.score(qs, out).items()):
    print(f"{kind:12s} n={n:2d} top1 {t1:.2f} top5 {t5:.2f} mrr {mrr:.2f}")
PY
```

Latency is the harness's own `perf_counter` round trip per `GET /api/search` over loopback, so it includes the Python client; the app's request log has no `/api/search` lines. The first query after an app start loads the vector cache (about 1 s, the run max) and is inside the 82 values.

**Short-prefix timing** (embeddings off): a queries file of 30 entries, `{"id","kind":"prefix","text","expected":[]}` with texts like `login re`, `password co`, `voucher ex` (15 words times two, each followed by a two-letter prefix), run with `scripts/search-eval.py run ... --label sp --prefix --queries-dir <dir> --results-dir $R`, then p95 from `latencyMs` the same way (30 values, the 28th sorted).

**Cleanup:** stop the app (kill its pid) and `psql -U postgres -c 'drop database search_b6b'`.
