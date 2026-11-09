-- Phone app sign-ins (SHIP-907). A device session is the bearer credential the app holds; a sign-in code is the single-use,
-- two-minute handoff from the web confirm page. Only SHA-256 hashes of secrets and codes are stored, never the secrets, the
-- verifier or the codes. Nothing is deleted: ending a session sets revoked_at, spending a code sets used_at.
CREATE TABLE device_session (
    id             uuid PRIMARY KEY,
    user_id        uuid NOT NULL REFERENCES app_user (id),
    name           text NOT NULL CHECK (length(name) BETWEEN 1 AND 100),
    token_hash     bytea NOT NULL,
    token_prefix   text NOT NULL,
    security_stamp text NOT NULL,
    created_at     timestamptz NOT NULL,
    last_used_at   timestamptz NOT NULL,
    expires_at     timestamptz NOT NULL,
    revoked_at     timestamptz
);
CREATE UNIQUE INDEX ux_device_session_token_hash ON device_session (token_hash);
CREATE INDEX ix_device_session_user_created ON device_session (user_id, created_at DESC);

CREATE TABLE device_sign_in_code (
    id             uuid PRIMARY KEY,
    code_hash      bytea NOT NULL,
    challenge      text NOT NULL,
    user_id        uuid NOT NULL REFERENCES app_user (id),
    security_stamp text NOT NULL,
    device_name    text NOT NULL,
    created_at     timestamptz NOT NULL,
    expires_at     timestamptz NOT NULL,
    used_at        timestamptz
);
CREATE UNIQUE INDEX ux_device_sign_in_code_code_hash ON device_sign_in_code (code_hash);
