-- GitHub issue conversations (SHIP-742, D9): a session either implements its item or, in propose mode, writes a
-- read-only proposal for a GitHub issue.
ALTER TABLE agent_session ADD COLUMN mode text NOT NULL DEFAULT 'implement' CHECK (mode IN ('implement', 'propose'));
