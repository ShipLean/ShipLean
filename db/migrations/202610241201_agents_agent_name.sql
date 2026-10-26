-- The agent (an MCP client acting for the signed-in user) that wrote a revision or comment, as
-- "Claude Code (claude.ai)" (verified host) or "Claude Code (unverified)". Null when a person wrote it.
ALTER TABLE revision ADD COLUMN agent_name text;
ALTER TABLE comment ADD COLUMN agent_name text;
