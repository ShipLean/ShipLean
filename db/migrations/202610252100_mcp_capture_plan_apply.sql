-- capture_plan (#43) applies a previewed plan at most once. The apply inserts one row here, in the same transaction as the plan's
-- stories and tasks, so a crash before the commit leaves no row and a second apply of the same preview (a retry after a
-- failed attach, a response lost to a timeout, two calls at once) conflicts on plan_id and finishes the first plan instead of
-- writing another. plan_id is the id the preview sealed into its plan token; feature_id is the Feature the plan landed under.
CREATE TABLE mcp_capture_plan_apply (
    plan_id    uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id),
    feature_id uuid NOT NULL REFERENCES work_item (id),
    applied_at timestamptz NOT NULL
);
