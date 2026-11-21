-- The name and version an MCP client sent in initialize, kept on the authorization (one per user and client) because the MCP
-- transport is stateless and refreshed or rotated tokens belong to the same row. Self-reported: for prompt sections and labels only.
ALTER TABLE oauth_authorization
    ADD COLUMN mcp_client_name    text NULL CHECK (length(mcp_client_name) <= 200),
    ADD COLUMN mcp_client_version text NULL CHECK (length(mcp_client_version) <= 200);
