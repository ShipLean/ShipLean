-- The Feature editor's fields on work_item. All are nullable (migrations run before the seed) and only a Feature may set them:
-- goal and status_line are the one-paragraph goal and the one-line status; status_verified_* record when the status line was
-- last written or confirmed, by whom, through which agent and MCP tool (null for a change made in the app); slug is the
-- Feature's short name, unique per project ignoring case, set at creation; started_on is the UTC day it was created.
ALTER TABLE work_item
    ADD COLUMN goal text,
    ADD COLUMN status_line text,
    ADD COLUMN status_verified_at timestamptz,
    ADD COLUMN status_verified_by uuid REFERENCES app_user (id),
    ADD COLUMN status_verified_agent text,
    ADD COLUMN status_verified_via text,
    ADD COLUMN slug text,
    ADD COLUMN started_on date;

ALTER TABLE work_item
    ADD CONSTRAINT ck_work_item_feature_fields_feature_only CHECK (
        type = 'feature'
        OR (goal IS NULL AND status_line IS NULL AND status_verified_at IS NULL AND status_verified_by IS NULL
            AND status_verified_agent IS NULL AND status_verified_via IS NULL AND slug IS NULL AND started_on IS NULL)),
    ADD CONSTRAINT ck_work_item_goal_length CHECK (goal IS NULL OR char_length(goal) <= 1000),
    ADD CONSTRAINT ck_work_item_status_line_one_line CHECK (status_line IS NULL OR (char_length(status_line) <= 500 AND status_line !~ '[\r\n]')),
    ADD CONSTRAINT ck_work_item_slug_format CHECK (slug IS NULL OR (char_length(slug) <= 64 AND slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'));

CREATE UNIQUE INDEX ux_work_item_slug ON work_item (project_id, lower(slug)) WHERE slug IS NOT NULL;
