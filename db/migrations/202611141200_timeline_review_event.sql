-- Review events on tasks (SHIP-1465 D15, contract C4). Additive and append-only: one row per review a reviewer logs, as many as
-- there are rounds, never edited or deleted. Written only by the log_review MCP tool, which also enforces that the item is a task;
-- read only by the timeline's facts reader. The reviewer is the agent when agent_name is set, else by_user_id.
CREATE TABLE review_event (
    id           uuid PRIMARY KEY,
    work_item_id uuid NOT NULL REFERENCES work_item (id),
    verdict      text NOT NULL CHECK (verdict IN ('approved', 'changesRequested')),
    note         text,
    by_user_id   uuid NOT NULL REFERENCES app_user (id),
    agent_name   text,
    at           timestamptz NOT NULL
);
CREATE INDEX ix_review_event_work_item ON review_event (work_item_id);
