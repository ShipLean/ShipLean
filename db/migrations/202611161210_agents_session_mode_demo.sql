-- Sprint demos made by an agent-relay agent (SHIP-1703, C1): a session ShipLean starts for a sprint demo is stored as mode 'demo', so the
-- relay runs it with the demo tool profile. The column's CHECK is Postgres's default-named agent_session_mode_check.
ALTER TABLE agent_session DROP CONSTRAINT agent_session_mode_check;
ALTER TABLE agent_session ADD CONSTRAINT agent_session_mode_check CHECK (mode IN ('implement', 'propose', 'demo'));
