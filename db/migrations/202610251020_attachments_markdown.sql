-- Markdown files up to 1 MB keep their text here so search can find them. An agent that attached a file leaves its name and the MCP tool it used.
ALTER TABLE attachment ADD COLUMN search_text text;
ALTER TABLE attachment ADD COLUMN agent_name text;
ALTER TABLE attachment ADD COLUMN uploaded_via text;
