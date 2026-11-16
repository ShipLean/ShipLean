-- A commit's time on its link (SHIP-1398 K1, item D12): the UTC author date of the commit, for lead time (start to last commit).
-- Additive and nullable: set only on url links whose title is a commit marker, by report_task's committed_at or, once and never
-- over a time already set, by the temporary backfill_commit_times tool. Every other link keeps it null.
ALTER TABLE work_item_link ADD COLUMN committed_at timestamptz;
COMMENT ON COLUMN work_item_link.committed_at IS
    'UTC author date of the commit this url link records; null for other links and for commits sent without a time.';
