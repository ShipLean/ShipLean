-- Inspection (SHIP-1463, K2): the per-project switch and the Tempo/Loki run-link cache. Nothing here references a work item table:
-- runs name their items by key string, so the module can be lifted out later.
CREATE TABLE inspection_project_settings (
    project_id        uuid PRIMARY KEY REFERENCES project (id),
    enabled           boolean NOT NULL DEFAULT false,
    show_content      boolean NOT NULL DEFAULT false,
    tenant_override   text,
    selector_override text,
    updated_at        timestamptz NOT NULL DEFAULT now()
);

-- Cache of the runs a Tempo/Loki query found for an item; rows older than ten minutes are ignored, never deleted.
CREATE TABLE inspection_run_links (
    project_key text NOT NULL,
    item_key    text NOT NULL,
    run_id      text NOT NULL,
    session_id  text NOT NULL,
    trace_ids   text[] NOT NULL DEFAULT '{}',
    source      text NOT NULL,
    summary     jsonb NOT NULL,
    fetched_at  timestamptz NOT NULL,
    PRIMARY KEY (project_key, item_key, run_id)
);
