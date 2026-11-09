-- A large tool argument an agent sends from a file (create_payload_upload), by payload id.
-- token_hash is the SHA-256 of the one-time upload URL token; the token itself is never stored. The payload id is not a
-- credential: a lookup must also match user_id, authorization_id (null for a gateway caller) and tool.
-- field null: body is a JSON object of the tool's arguments; otherwise body is the raw UTF-8 text of that one argument.
-- upload_by ends the upload URL; uploaded_at, expires_at (60 minutes after upload), size_bytes and body are set once by the upload.
-- Expired bodies are cleared with UPDATE ... SET body = NULL (no hard deletes).
CREATE TABLE tool_payload (
    id               text PRIMARY KEY,
    token_hash       bytea NOT NULL UNIQUE,
    tool             text NOT NULL,
    field            text,
    user_id          uuid NOT NULL REFERENCES app_user (id),
    authorization_id uuid REFERENCES oauth_authorization (id),
    agent_stamp      text NOT NULL,
    created_at       timestamptz NOT NULL,
    upload_by        timestamptz NOT NULL,
    uploaded_at      timestamptz,
    expires_at       timestamptz,
    size_bytes       int,
    body             bytea
);
CREATE INDEX ix_tool_payload_expires_at ON tool_payload (expires_at) WHERE body IS NOT NULL;
