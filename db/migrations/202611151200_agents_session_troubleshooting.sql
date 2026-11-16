-- Agent session troubleshooting (SHIP-1620, contract C1): why a session ended, every delivery attempt, the agent's run log,
-- and the one-use transfer link an agent uploads that log through. Nothing is deleted.

-- end_reason is written with the state by every writer (NULL while live). There is no state-to-reason CHECK yet: older
-- instances running during a deploy and existing test seeds write ended sessions without one; a guard test holds the rule.
ALTER TABLE agent_session
    ADD COLUMN end_reason text CHECK (end_reason IN ('completed', 'canceled', 'deliveryFailed', 'agentError', 'stale', 'proposalWithoutIssue', 'unknown')),
    ADD COLUMN end_detail text CHECK (char_length(end_detail) <= 1000),
    ADD COLUMN run_log_attachment_id uuid REFERENCES attachment (id);

-- Sessions that ended before this migration: the reason was never recorded.
UPDATE agent_session SET end_reason = 'unknown' WHERE state IN ('completed', 'failed', 'canceled', 'stale');

-- One row per send, success or not (a 2xx and a null error on success). A resend after a failed sent-mark reuses the attempt
-- number, so writers insert ON CONFLICT (delivery_id, attempt) DO NOTHING. error is scrubbed before it is stored.
CREATE TABLE agent_delivery_attempt (
    id              uuid PRIMARY KEY,
    delivery_id     uuid NOT NULL REFERENCES agent_delivery (id),
    connection_id   uuid NOT NULL REFERENCES agent_connection (id),
    attempt         integer NOT NULL CHECK (attempt >= 1),
    at              timestamptz NOT NULL,
    http_status     integer,
    error           text CHECK (char_length(error) <= 1000),
    duration_ms     integer NOT NULL CHECK (duration_ms >= 0),
    next_attempt_at timestamptz,
    UNIQUE (delivery_id, attempt)
);
CREATE INDEX ix_agent_delivery_attempt_delivery ON agent_delivery_attempt (delivery_id);
CREATE INDEX ix_agent_delivery_attempt_connection ON agent_delivery_attempt (connection_id, at DESC);

-- 'agentLog': one per session, issued at start, used once by the agent to upload its run log. session_id ties it to the session.
ALTER TABLE transfer_token DROP CONSTRAINT transfer_token_kind_check;
ALTER TABLE transfer_token ADD CONSTRAINT transfer_token_kind_check CHECK (kind IN ('upload', 'download', 'demo', 'demoLogo', 'agentLog'));
ALTER TABLE transfer_token ADD COLUMN session_id uuid REFERENCES agent_session (id);
