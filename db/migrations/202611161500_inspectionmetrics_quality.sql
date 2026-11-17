-- Inspection quality (SHIP-1398 QK1). Three additive changes:
-- 1. Found before release on a bug (item D17): a bug marked so is not an escaping bug. Settable on bugs only (the app checks the
--    type); every existing row and every other type stays false.
ALTER TABLE work_item ADD COLUMN found_before_release boolean NOT NULL DEFAULT false;
COMMENT ON COLUMN work_item.found_before_release IS
    'Bugs only: true when the bug was found before release, so Inspection does not count it as escaping.';

-- 2. The Caused by link type (item D18): a link from a Bug to the Task that introduced it. The app allows only Bug -> Task.
ALTER TABLE work_item_link DROP CONSTRAINT work_item_link_link_type_check;
ALTER TABLE work_item_link ADD CONSTRAINT work_item_link_link_type_check
    CHECK (link_type IN ('predecessor', 'successor', 'blockedBy', 'related', 'duplicate', 'url', 'causedBy'));

-- 3. Bug severity weights (item D16): severity (1 = critical .. 4 = low) to its weight in the weighted escaping-bug count.
ALTER TABLE project ADD COLUMN bug_severity_weights jsonb NOT NULL DEFAULT '{"1":8,"2":4,"3":2,"4":1}';
COMMENT ON COLUMN project.bug_severity_weights IS
    'Severity (1-4, as text keys) to weight for the weighted escaping-bug count; edited on Settings > General by a project admin.';
