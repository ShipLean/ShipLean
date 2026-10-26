# Corpus format (CA1)

One file per project: `corpus/<project>.json`, validated by `corpus.schema.json` and `validate.py`.

```
{
  project: {ref, key, name, description},
  sprints: [{ref, name, goal, startDate, endDate}],
  items: [{
    ref, type, title, description, acceptanceCriteria[], reproSteps, tags[], state, parentRef?, sprintRef?,
    comments: [{ref, body, archived}]
  }]
}
```

- `ref` is corpus-local (`shop-017`) and unique across every corpus file, including sprints and comments.
- `type` is lowercase: `epic`, `feature`, `story`, `bug`, `task`. `state` is one of the default state names: `New`, `Active`, `Resolved`, `Closed`, `Removed`.
- Dates are `YYYY-MM-DD`. There are no archived sprints (no endpoint archives one).
- **Item keys.** The n-th entry of `items` (1-based) becomes `<KEY>-n`. The importer creates items in file order and sets parents afterwards, so a `parentRef` must name an item *earlier* in the same file.
- Items are listed oldest first; the ILIKE baseline treats later items as more recently changed.
- Project keys and refs, and query ids, are unique across all files. A query id starts with its project ref and a dash (`shop-q01`).
- Sprint names are unique per project ignoring case.

`validate.py` also enforces what the REST import would reject:

- the project key matches `^[A-Z]{2,4}$` and is not a seeded key (`SHIP PAY MOB DATA OPS PART ARCH EMPT`);
- each (type, parent type) pair is allowed (Epic <- Feature <- Feature|Story|Bug, Story|Bug <- Task) and parents have no cycles;
- state names exist in the defaults; `sprintRef` names a sprint of the same file;
- sprint dates do not overlap, sprint names are unique ignoring case, and `endDate >= startDate`;
- refs are unique across all files.

Queries live in `queries/<project>.json`: `[{id, text, kind, expected: [ref...]}]`, where `kind` is one of
`exactKey`, `titleWord`, `paraphrase`, `bodyOnly`, `commentOnly`, `prefix`, `nonEnglish`.

- an `exactKey` query's text names the key of an expected item (`DEMO-4`, `demo4` and `demo-4` all do);
- `expected` never lists a Removed item (never indexed). Closed items are allowed: `/api/search` hides them unless `closed=true`, which A4's `run` passes.

Only the five real projects live in `corpus/` and `queries/`. The tooling sample is `sample/corpus.json` + `sample/queries.json` and is not part of the corpus A5 and B6b count.

Rankings files (`results/<label>.json`): `{label, queries: {<id>: [[ref, score|null]…]}, latencyMs: {<id>: ms}}`; `latencyMs` values are numbers with decimals (milliseconds).

Run `python validate.py [files...]` (default: every corpus file with its queries, then the sample) and `python metrics.py results/<label>.json...`.
