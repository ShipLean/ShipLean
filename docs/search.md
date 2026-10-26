# Search

Advanced search (#35): one `search_doc` table holds the searchable text, a lexical leg reads its `tsv`, a semantic leg reads its `embedding`, and `ISearchRanker` fuses them. This file is the row contract; every slice that writes or reads `search_doc` follows it.

## Table

`search_doc` (migration `202610261200_search_search_doc.sql`) has one row per chunk.

| Column | Meaning |
|---|---|
| `id` | Row id. |
| `project_id` | The project the text belongs to. Every read joins `project_member` for the current user. |
| `source_kind` | `item`, `comment`, `attachment`, `project` or `sprint` (`SearchSourceKind`). |
| `source_id` | The id of the item, comment, attachment, project or sprint. |
| `work_item_id` | The item the text belongs to: the item itself for an item, the owning item for a comment or attachment, null for a project or sprint. |
| `chunk_no` | 0 for a field that fits one chunk. Unique on `(source_kind, source_id, field, chunk_no)`. |
| `field` | `title`, `tags`, `body`, `comment`, `attachment`, `project`, `sprint_goal`, `sprint_name` (`SearchField`; `SearchDocColumns` maps the enums to these strings). |
| `text` | The chunk text, exactly what is embedded (after `passage: `) and what snippets are cut from. |
| `text_hash` | SHA-256 hex of `text`. |
| `tsv` | Generated and stored; never write it, never map it into a Dapper record. |
| `embedding` | `real[]` (`float[]`), L2-normalised, null until embedded. |
| `embedding_model` | `name@revision` of the model that wrote `embedding` (`EmbeddingModelId.ToString()`). |
| `indexed_at` | When the row was written. |

The table is derived, so it is the one table the app may `DELETE FROM` (`NoHardDeletesTests.Exempt`).

## tsv

`tsv = setweight(to_tsvector('english', t), w) || setweight(to_tsvector('simple', t), w)`, where `t` is `text` for every field except `body`, `comment` and `attachment`, where it is the text after the first newline (`substr(text, strpos(text, E'\n') + 1)`, the whole text when there is no newline). The first line of those rows is the item title prefix (§ Chunking): it is in `text` and the embedding but not in `tsv` or in snippets, so a title match is not also a body-list match. The `english` half stems words; the `simple` half keeps keys (`SHIP-12`) and identifiers exactly. Query with `websearch_to_tsquery('english', q) || websearch_to_tsquery('simple', q)`: the words are ANDed when the semantic leg can answer (an OR costs the hybrid ranking about 8 points of top-5 accuracy in the spike). When it cannot (embeddings off or the model failed to load: `CandidateQuery.Vector == null`), the item leg also returns an any-word list, `BodyLexicalAny`, over the body, comment and attachment rows: the same words joined with `or`, read with the `english` config only (the `simple` half keeps stop words, so "the" and "to" would match nearly every row: about 80 ms to rank on the 498-item eval corpus, which took the fallback's p95 from 128-142 ms to 57-73 ms), fused by RRF with the AND lists. Titles stay AND. A query with a quote, a negation or its own `or`, or with a single word, gets no any-word list (`SearchTsQuery.AnyWords`). When the caller is typing (`Prefix`), the last plain word becomes `'word':*` and is ANDed with the rest (in the any-word list it is ORed with the others, and left out when it is shorter than 3 characters, so "login r" does not OR in every `r*` row); that split lives in `SearchTsQuery`, shared by the item leg and the project and sprint groups. The weight comes from `field`:

| Weight | Fields |
|---|---|
| A | `title`, `project` |
| B | `tags` |
| C | `body`, `attachment`, `sprint_goal` |
| D | `comment`, `sprint_name` |

Weight D is Postgres' default and is not printed in the `tsvector` text.

## What goes in each field

| Source | Field | Text | Condition |
|---|---|---|---|
| item | `title` | The title (one chunk, no prefix). | Item not removed (`state.category <> 'removed'`). |
| item | `tags` | The tags joined with a space. | At least one tag. |
| item | `body` | Description, then each acceptance criterion, then repro steps, as Markdown source with blank lines between. | Non-blank. |
| comment | `comment` | The comment body. | Not archived, on an item that qualifies. |
| attachment | `attachment` | `attachment.search_text`. | `search_text IS NOT NULL`, not archived, on an item that qualifies. |
| project | `project` | Key, a space, name, a newline, then the description (`SHIP ShipLean\n<description>`), so a key-only query matches the project group. | Always (projects have no `archived_at`). |
| sprint | `sprint_name` | The name. | Not archived. |
| sprint | `sprint_goal` | The goal. | Not archived, non-blank. |

An item "qualifies" when it is not removed. "Removed" always means `state.category = 'removed'` (as `OrientReads` filters), never a state name: names are per project and can change.

A removed item's own rows are dropped. Its comment and attachment rows are not: the writer does not delete them when the item drops out. Every candidate query (B2a, B3a, B3b) joins `work_item` and `state` and leaves out removed items, so those rows are invisible, and they are visible again once the item is restored. A restore is a `WorkItemUpdated` with a state change, which re-syncs the item's comment and attachment sources as well as the item itself (their chunks carry the item title as prefix), so nothing is left stale.

## Embedded and lexical-only fields

| Embedded | Lexical only |
|---|---|
| `title`, `body`, `comment`, `attachment`, `sprint_goal` | `tags`, `project`, `sprint_name` |

`SearchFields.IsEmbedded(SearchField)` (Contracts) is the one rule; B2b and B3b share it. Lexical-only rows are never stale under the model rule and never count as pending (`CandidateLists.PendingRows`, `IndexPassResult.Remaining`). `ix_search_doc_unembedded` covers only the embedded fields.

## Chunking

- `title`, `tags`, `project`, `sprint_name` and `sprint_goal` are one chunk (`chunk_no` 0).
- `body`, `comment` and `attachment` text is split at about `SHIPLEAN_SEARCH_CHUNK_CHARS` (1,200): on paragraph boundaries first, then a paragraph longer than the limit on sentences, then a sentence longer than the limit is cut at the limit (never between the halves of a surrogate pair). `SHIPLEAN_SEARCH_CHUNK_OVERLAP` (150) characters, moved forward to a word start, are repeated at the start of the next chunk. The limit counts the field text only, not the title prefix, so a chunk can run to about limit + overlap characters plus the prefix. `chunk_no` counts 0, 1, 2… per `(source, field)`.
- Every `body`, `comment` and `attachment` chunk starts with the item's title and a newline, so the chunk keeps its context. The prefix is part of `text`, of `text_hash` and of the embedding, but not of `tsv` (§ tsv) and snippets are cut from the text after it. A source supplies the title in `SearchSourceText.ItemTitle`; the writer adds it.
- A project and a sprint never get a prefix.

## Hash and staleness

- `text_hash` is the SHA-256 hex of `text` (`SearchDocBuilder.HashOf`).
- A row is stale when the hash of the text the source now produces differs from `text_hash`, or, for an embedded field only, when `embedding_model` differs from the loaded model's id. A renamed item, an import and a model swap therefore need no invalidation code.
- Writing changed text clears `embedding` and `embedding_model` and sets `indexed_at`. Unchanged rows are left alone, so `indexed_at` only moves when something changed: the text, or a vector written for it (the semantic leg's cache key is `(max(indexed_at), count(*))` per project: the count catches deleted rows, which don't move `max(indexed_at)`; the embedding pass sets `indexed_at` when it writes a vector, so the key also moves when vectors arrive).
- A source that no longer produces a field, or a chunk past the new last chunk, has its rows deleted.

## Never indexed

- Removed items (their comments and attachments stay invisible, see above); archived comments, attachments and sprints.
- Attachments without `search_text` (images, binary files).
- Revisions, link cards, user names and emails, board settings.
- Anything of a project the caller is not a member of is never returned: every candidate query joins `project_member`.

## Pieces

- `IEmbedder` (Contracts): `ModelId`, `Dimensions`, `Status { Mode, Reason }`, `EmbedQueryAsync`, `EmbedPassagesAsync`. `NoEmbedder` is the lexical one: `ModelId` null, `Dimensions` 0, both embed methods throw, so check `Status` first. `EmbedderServices.Register` is the production factory.
- The ranker embeds the query once, and only when `IEmbedder.Status.Mode` is semantic, and passes the vector as `CandidateQuery.Vector`. A semantic leg or group given `Vector == null` returns `Available = false` (groups: no semantic sprint matches).
- `SearchResultItem.ParentPath` is the keys of the ancestors, top-down (`["SHIP-2", "SHIP-7"]`), matching MCP's `SearchHit.Parent` and the eval fixture; empty for a top-level item.
- `SHIPLEAN_EMBEDDINGS` accepts only `on` or `off`; any other value throws when `SearchOptions` is resolved, and B1's factory must not swallow it. `SHIPLEAN_SEARCH_CHUNK_OVERLAP` must be less than `SHIPLEAN_SEARCH_CHUNK_CHARS`, else it throws too.
- `ISearchIndexer.RunPassAsync(full)` (`SearchIndexer`) is one synchronous pass. It loops over batches (`SHIPLEAN_SEARCH_BATCH`, claimed `FOR UPDATE SKIP LOCKED`) until nothing is claimable, and the claim is bounded by the pass start (`indexed_at <= now()` taken when the pass begins, after a full sync), so rows written meanwhile wait for the next pass. `Remaining` = rows still pending afterwards (rows locked by another pass, and rows written during this pass, which the bound leaves for the next; 0 when the embedder is lexical). `Synced` = distinct sources in `search_doc` after a full sync, else 0.
- `SearchIndexHostedService` runs the passes in the background: the first after `SHIPLEAN_SEARCH_FIRST_PASS_SECONDS` is a full pass (a full sync first, so data that predates the index is written; the writer only rewrites changed hashes), then one every `SHIPLEAN_SEARCH_INTERVAL_SECONDS` and one early when an item, comment, attachment, project or sprint event arrives; every later pass is incremental, after syncing the sources the events name (a work item event names the item and its comment and attachment sources). The background loop never runs a full sync again. Sources whose sync fails twice in a row are dropped with an error; a full reindex recovers them.
- `ISearchTextSource` (one per kind), `ISearchDocWriter`, `ISearchIndexer` (Contracts) and `SearchOptions` (Application, keys `SHIPLEAN_SEARCH_*` in README § Configuration) are the index side.
- `ISearchRanker` (Contracts) with `IItemCandidates` and `ISearchGroups` (Application `Features/Search/Ranking`) are the read side. A leg returns up to `SHIPLEAN_SEARCH_TOP_PER_LIST` hits per `RankedList`, best first, one hit per item.
- `PendingRows` (semantic leg) counts the unembedded or other-model rows of embedded fields (`SearchFields.IsEmbedded`) that belong to the items passing the filters (access, not removed, closed, types, tags, criteria).
- The semantic vector cache (`SearchVectorCache`, singleton) holds every loaded project's embedded rows in memory at about 1.7 KB per row (about 85 MB at 50k rows) and has no eviction. A project's entry is reloaded when its key changes: `(max(indexed_at), count(*))` plus the number of rows embedded with the loaded model. Each query reads that grouped key and the pending count from `search_doc`; both scan the member projects' rows (tens of ms at 50k rows).
- `SearchGroups` opens its own connection per call, so the ranker runs the project and sprint group queries concurrently.
- The cosine cut-off (`SHIPLEAN_SEARCH_CUTOFF`) belongs to one model and pooling pair: e5-small int8 with mean pooling, where the spike measured 0.83 on the semantic leg alone. Against the real ranker (RRF with the lexical lists, boosts, 498-item corpus) the default is **0.84**: all-kinds top-5 is 0.93-0.96 from 0.82 to 0.85 and falls off a cliff above it (paraphrase top-5 0.93 at 0.85, 0.71 at 0.86, 0.36 at 0.87), so 0.84 keeps a full step of margin below the cliff (0.85 scores about the same), and below it the semantic lists flood the fusion (bodyOnly 0.79 at 0.80). Table and runs: `scripts/search-eval/RESULTS-hybrid.md`. Changing the model or the pooling means measuring the cut-off again.
- Tests arrange rows with `SearchDocBuilder` (no events, no indexer) and write results shapes in `src/ShipLean.TestSupport/DtoFixtures/search/`.

## Quick search

The quick search dialog (`docs/navigation.md` § Quick search) calls `GET /api/search` in typing mode (`prefix=true`) with closed items included (`closed=true`). The client puts the open project's results first, keeping the server's order within each part. Besides the existing fields, the view carries `stateCategory` on items and `status` and `datesLabel` on sprints, which the dialog shows as the state dot and the sprint's dates. The query text is never logged or traced.
