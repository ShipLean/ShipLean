-- Agent (MCP) sign-in: an OAuth 2.1 authorization server for public clients with PKCE. Secrets are never stored:
-- codes and tokens are kept as SHA-256 hashes. Nothing is deleted; an authorization or token is revoked_at.

-- A client an agent connects as: a metadata document URL (kind metadataDocument) or a dynamic registration (registered).
CREATE TABLE oauth_client (
    client_id           text PRIMARY KEY,
    kind                text NOT NULL CHECK (kind IN ('metadataDocument', 'registered')),
    client_name         text NOT NULL,
    client_host         text,
    redirect_uris       text[] NOT NULL CHECK (cardinality(redirect_uris) > 0),
    metadata_expires_at timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    archived_at         timestamptz
);

-- A pending /oauth/authorize request, kept while the person signs in and consents. Rows are never deleted, so decided_at
-- marks a request as used: it can be decided once.
CREATE TABLE oauth_request (
    id             uuid PRIMARY KEY,
    client_id      text NOT NULL REFERENCES oauth_client (client_id),
    redirect_uri   text NOT NULL,
    state          text,
    code_challenge text NOT NULL,
    scopes         text[] NOT NULL,
    resource       text NOT NULL,
    user_id        uuid REFERENCES app_user (id),
    expires_at     timestamptz NOT NULL,
    decided_at     timestamptz
);
CREATE INDEX ix_oauth_request_client_id ON oauth_request (client_id);
CREATE INDEX ix_oauth_request_expires_at ON oauth_request (expires_at);

-- One person's consent to one client; its tokens live and die with it. security_stamp is the user's stamp at consent.
CREATE TABLE oauth_authorization (
    id             uuid PRIMARY KEY,
    user_id        uuid NOT NULL REFERENCES app_user (id),
    client_id      text NOT NULL REFERENCES oauth_client (client_id),
    scopes         text[] NOT NULL,
    security_stamp text NOT NULL,
    created_at     timestamptz NOT NULL DEFAULT now(),
    last_used_at   timestamptz,
    revoked_at     timestamptz
);
CREATE INDEX ix_oauth_authorization_user_id ON oauth_authorization (user_id);
CREATE INDEX ix_oauth_authorization_client_id ON oauth_authorization (client_id);
-- One live authorization per person and client: a re-consent updates it instead of adding a second row.
CREATE UNIQUE INDEX ux_oauth_authorization_live_user_client ON oauth_authorization (user_id, client_id) WHERE revoked_at IS NULL;

-- A one-time authorization code (60 seconds), by SHA-256 hash.
CREATE TABLE oauth_code (
    code_hash        bytea PRIMARY KEY,
    authorization_id uuid NOT NULL REFERENCES oauth_authorization (id),
    code_challenge   text NOT NULL,
    redirect_uri     text NOT NULL,
    resource         text NOT NULL,
    expires_at       timestamptz NOT NULL,
    used_at          timestamptz
);
CREATE INDEX ix_oauth_code_authorization_id ON oauth_code (authorization_id);

-- An access or refresh token, by SHA-256 hash. Tokens issued from one code or refresh chain share a family_id.
CREATE TABLE oauth_token (
    token_hash       bytea PRIMARY KEY,
    kind             text NOT NULL CHECK (kind IN ('access', 'refresh')),
    authorization_id uuid NOT NULL REFERENCES oauth_authorization (id),
    family_id        uuid NOT NULL,
    resource         text NOT NULL,
    expires_at       timestamptz NOT NULL,
    rotated_at       timestamptz,
    revoked_at       timestamptz
);
CREATE INDEX ix_oauth_token_authorization_id ON oauth_token (authorization_id);
CREATE INDEX ix_oauth_token_family_id ON oauth_token (family_id);
