-- Prompts over MCP (SHIP-2231 C1): a version saved by an agent through save_prompt has source 'mcp' and the agent stamp
-- (AgentStamp.Of) in agent_name; web saves leave agent_name null.
ALTER TABLE prompt_version DROP CONSTRAINT prompt_version_source_check;
ALTER TABLE prompt_version ADD CONSTRAINT prompt_version_source_check
    CHECK (source IN ('editor', 'builtInUpdate', 'migrated', 'mcp'));
ALTER TABLE prompt_version ADD COLUMN agent_name text;
