-- Archived videos lose their bytes (SHIP-2338): ArchivedVideoSweeper deletes the blob of an archived video/* attachment and stamps
-- blob_deleted_at. The row stays. Every other type keeps its blob, so this stays null for them.
ALTER TABLE attachment ADD COLUMN blob_deleted_at timestamptz;
CREATE INDEX ix_attachment_video_blob_due ON attachment (archived_at)
    WHERE archived_at IS NOT NULL AND blob_deleted_at IS NULL AND content_type LIKE 'video/%';
