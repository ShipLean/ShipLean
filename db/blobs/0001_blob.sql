-- Blob store on Postgres (#36): one row per blob. These scripts live apart from db/schema and db/migrations because
-- SHIPLEAN_BLOB_DB_CONNECTION may name a database of its own, which those never reach; PostgresBlobStore.ApplySchema runs
-- them, journaled in blob_schemaversions. Nothing is deleted: a put replaces the row's bytes.
CREATE TABLE blob (
    key          text PRIMARY KEY,
    content_type text NOT NULL,
    bytes        bytea NOT NULL,
    size_bytes   bigint NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now()
);
