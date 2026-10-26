-- A replaced connection no longer keeps its token: new replacements clear it, this clears the ones archived before that.
UPDATE jira_connection SET api_token_encrypted = NULL WHERE archived_at IS NOT NULL AND api_token_encrypted IS NOT NULL;
