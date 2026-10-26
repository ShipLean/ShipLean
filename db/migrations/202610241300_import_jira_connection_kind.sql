-- A Jira connection signs in by its kind: cloud = email + API token (Basic), server = personal access token (Bearer).
ALTER TABLE jira_connection ADD COLUMN kind text NOT NULL DEFAULT 'cloud' CHECK (kind IN ('cloud', 'server'));

-- One current connection per project; a replaced one keeps its row with archived_at set.
CREATE UNIQUE INDEX ux_jira_connection_current ON jira_connection (project_id) WHERE archived_at IS NULL;
