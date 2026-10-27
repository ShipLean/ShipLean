-- A short-lived URL token for moving a file between an agent's shell and an attachment, by SHA-256 hash.
-- kind 'upload' (one use: used_at is set once the attachment is stored) or 'download' (attachment_id set, reusable until it expires).
-- authorization_id is the agent's OAuth authorization when an agent asked for the URL; revoking it ends unused tokens.
CREATE TABLE transfer_token (
    token_hash       bytea PRIMARY KEY,
    kind             text NOT NULL CHECK (kind IN ('upload', 'download')),
    work_item_id     uuid NOT NULL REFERENCES work_item (id),
    attachment_id    uuid REFERENCES attachment (id),
    name             text NOT NULL,
    content_type     text NOT NULL,
    user_id          uuid NOT NULL REFERENCES app_user (id),
    authorization_id uuid REFERENCES oauth_authorization (id),
    agent_stamp      text NOT NULL,
    expires_at       timestamptz NOT NULL,
    used_at          timestamptz
);
CREATE INDEX ix_transfer_token_work_item_id ON transfer_token (work_item_id);
