-- Agent dispatch (SHIP-178 B2): the state flag that starts an agent, the session a flagged move opens, the activity log the
-- agent writes into it, and the outbox of webhooks ShipLean owes the connection. Nothing is deleted.
ALTER TABLE state ADD COLUMN starts_agent boolean NOT NULL DEFAULT false;

-- One dispatch record per session. repo_url and base_branch are the project's values when the session started, so a later
-- `prompted` event names the repository the agent is working in. external_urls is a jsonb array of {url, label}; plan is a
-- jsonb array of {content, status}. last_activity_at starts at created_at and moves with every activity (the stale sweep).
CREATE TABLE agent_session (
    id                uuid PRIMARY KEY,
    work_item_id      uuid NOT NULL REFERENCES work_item (id),
    connection_id     uuid NOT NULL REFERENCES agent_connection (id),
    state             text NOT NULL CHECK (state IN ('submitted', 'working', 'inputRequired', 'completed', 'failed', 'canceled', 'stale')),
    repo_url          text NOT NULL,
    base_branch       text NOT NULL,
    external_urls     jsonb NOT NULL DEFAULT '[]' CHECK (jsonb_typeof(external_urls) = 'array'),
    plan              jsonb NOT NULL DEFAULT '[]' CHECK (jsonb_typeof(plan) = 'array'),
    last_activity_at  timestamptz NOT NULL,
    stop_requested_at timestamptz,
    created_by        uuid NOT NULL REFERENCES app_user (id),
    created_at        timestamptz NOT NULL,
    ended_at          timestamptz
);
-- At most one live session per item: two instances racing to start one, one of them loses.
CREATE UNIQUE INDEX ux_agent_session_live ON agent_session (work_item_id) WHERE state IN ('submitted', 'working', 'inputRequired');
CREATE INDEX ix_agent_session_work_item ON agent_session (work_item_id, created_at DESC);
CREATE INDEX ix_agent_session_connection ON agent_session (connection_id);

CREATE TABLE agent_activity (
    id         uuid PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES agent_session (id),
    type       text NOT NULL CHECK (type IN ('thought', 'action', 'elicitation', 'response', 'error', 'prompt')),
    body       text,
    action     text,
    parameter  text,
    result     text,
    ephemeral  boolean NOT NULL DEFAULT false,
    signal     text,
    created_at timestamptz NOT NULL
);
CREATE INDEX ix_agent_activity_session ON agent_activity (session_id, created_at);

-- The outbox. id is the Linear-Delivery header. seq orders a session's rows (1 is `created`; the next writer takes max + 1
-- while holding the session row's lock) and a session's deliveries go out strictly in that order. claimed_until is the lease
-- a sender holds on a row it took with FOR UPDATE SKIP LOCKED. body is the payload without webhookTimestamp and webhookId.
CREATE TABLE agent_delivery (
    id              uuid PRIMARY KEY,
    session_id      uuid NOT NULL REFERENCES agent_session (id),
    seq             integer NOT NULL CHECK (seq >= 1),
    action          text NOT NULL CHECK (action IN ('created', 'prompted')),
    body            jsonb NOT NULL,
    attempts        integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at timestamptz NOT NULL,
    claimed_until   timestamptz,
    status          text NOT NULL CHECK (status IN ('pending', 'sent', 'failed')),
    last_error      text,
    created_at      timestamptz NOT NULL,
    UNIQUE (session_id, seq)
);
CREATE INDEX ix_agent_delivery_pending ON agent_delivery (next_attempt_at) WHERE status = 'pending';
