-- The agent connection a project dispatches to, and the repository the agent works in (SHIP-178 A1). A null base branch means main.
ALTER TABLE project
    ADD COLUMN agent_connection_id uuid REFERENCES agent_connection (id),
    ADD COLUMN repo_url            text,
    ADD COLUMN repo_base_branch    text;
