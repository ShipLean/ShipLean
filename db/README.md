Apply order: `schema/` -> `migrations/` -> `seed/`, each folder in file-name order, via DbUp (`ShipLean.Application.Database`).
The app applies schema + migrations at startup (`SHIPLEAN_DB_MIGRATE`, default true); test fixtures also apply the seed.

## Schema (`schema/0001_tables.sql`)

Twenty tables, no others: `instance_settings` (one row), `app_user`, `user_preference`, `data_protection_key`, `project`,
`project_member`, `state`, `work_item`, `work_item_link`, `sprint`, `sprint_capacity`, `board`, `comment`, `attachment`,
`revision`, `jira_connection`, `import_job`, `import_mapping`, `import_error`, `import_log`.

- Work item type is a `text` column (`epic|feature|story|bug|task`); tags are `work_item.tags text[]`.
- Board columns, state mappings, swimlanes, card fields, style rules, tag colours and annotations live in `board.settings`
  (jsonb, the pinned shapes in `src/ShipLean.TestSupport/PinnedShapes`). `work_item.board_column_id` and `swimlane_id` are
  therefore `text` without a foreign key.
- The parent-type rule is a composite foreign key `(parent_id, parent_type)` -> `work_item(id, type)` plus a `CHECK` on the
  allowed `(type, parent_type)` pairs. Every parent is optional.
- A work item's state must belong to its project and type, and its sprint to its project (composite foreign keys).
- Enums are `text` + `CHECK` with the camelCase strings the persisted JSON uses.
- `backlog_order` and `column_order` are `text COLLATE "C"` ranks; the seed uses zero-padded numbers.
- Sprint status (past / current / future) is not stored: it is derived from the dates.
- Nothing is hard-deleted: `archived_at` (comment, attachment, work_item_link, sprint, project_member, jira_connection) or the Removed state category.

## Blobs (`blobs/`)

`blobs/*.sql` is not part of the app schema. It creates the `blob` table for the Postgres blob store
(`SHIPLEAN_BLOB_DB_CONNECTION`), which may be the app's database or one of its own, so `PostgresBlobStore.ApplySchema` applies it
with its own DbUp journal (`blob_schemaversions`), not `schemaversions`. `DatabaseScripts` never loads it.
