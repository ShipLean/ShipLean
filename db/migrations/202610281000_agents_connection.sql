-- Linear-compatible agent connections an instance admin adds in Instance settings > Agents (SHIP-178 A1). ShipLean signs the
-- webhook it sends to receiver_url with the signing secret, so that secret is stored encrypted (ASP.NET Data Protection,
-- purpose of its own); the callback token the agent calls back with is stored only as a SHA-256 hash. The last four
-- characters of each are kept beside them so the screen can show them without decrypting. Nothing is deleted: archived_at.
CREATE TABLE agent_connection (
    id                       uuid PRIMARY KEY,
    type                     text NOT NULL CHECK (type IN ('linear-compatible')),
    name                     text NOT NULL,
    receiver_url             text NOT NULL,
    signing_secret_protected text NOT NULL,
    signing_secret_last4     text NOT NULL,
    callback_token_hash      bytea NOT NULL,
    callback_token_last4     text NOT NULL,
    created_by               uuid REFERENCES app_user (id),
    created_at               timestamptz NOT NULL,
    updated_by               uuid REFERENCES app_user (id),
    updated_at               timestamptz NOT NULL,
    archived_at              timestamptz
);
CREATE UNIQUE INDEX ux_agent_connection_name ON agent_connection (lower(name)) WHERE archived_at IS NULL;
CREATE INDEX ix_agent_connection_callback_token_hash ON agent_connection (callback_token_hash);
