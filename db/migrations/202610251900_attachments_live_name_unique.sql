-- A name is live on a work item at most once: a re-upload archives the old row, and the index now holds the store to it.
-- Older duplicates are archived first, keeping the newest, the same as a replace (nothing is deleted).
ALTER TABLE attachment ADD COLUMN IF NOT EXISTS sanitized boolean NOT NULL DEFAULT false;

UPDATE attachment a
SET archived_at = now()
FROM (
    SELECT id, row_number() OVER (PARTITION BY work_item_id, name ORDER BY created_at DESC, id DESC) AS position
    FROM attachment
    WHERE archived_at IS NULL
) ranked
WHERE a.id = ranked.id AND ranked.position > 1;

CREATE UNIQUE INDEX IF NOT EXISTS ux_attachment_live_name ON attachment (work_item_id, name) WHERE archived_at IS NULL;
