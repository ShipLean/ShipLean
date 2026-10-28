-- Agent dispatch (SHIP-187): an ephemeral activity is replaced by the session's next activity. Nothing is deleted, so the
-- replaced row is marked and every read of the log leaves it out.
ALTER TABLE agent_activity ADD COLUMN replaced_at timestamptz;
