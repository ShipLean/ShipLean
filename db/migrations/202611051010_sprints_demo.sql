-- Sprint demos (SHIP-303, contract K3). Additive: the demo's state is scalars and pointers on the sprint row; its large values are attachments on a BAU item.
ALTER TABLE sprint
    ADD COLUMN demo_work_item_id           uuid REFERENCES work_item (id),
    ADD COLUMN demo_video_attachment_id    uuid REFERENCES attachment (id),
    ADD COLUMN demo_script_attachment_id   uuid REFERENCES attachment (id),
    ADD COLUMN demo_chapters_attachment_id uuid REFERENCES attachment (id),
    ADD COLUMN demo_duration_seconds       numeric(9,2),
    ADD COLUMN demo_requested_by           uuid REFERENCES app_user (id),
    ADD COLUMN demo_requested_via          text CHECK (demo_requested_via IN ('close', 'button', 'mcp')),
    ADD COLUMN demo_requested_at           timestamptz,
    ADD COLUMN demo_upload_expires_at      timestamptz,
    ADD COLUMN demo_delivered_at           timestamptz,
    ADD COLUMN demo_blocked_message        text,
    ADD COLUMN demo_youtube_state          text NOT NULL DEFAULT 'none' CHECK (demo_youtube_state IN ('none', 'pending', 'published', 'failed')),
    ADD COLUMN demo_youtube_video_id       text,
    ADD COLUMN demo_youtube_privacy        text CHECK (demo_youtube_privacy IN ('public', 'unlisted', 'private')),
    ADD COLUMN demo_youtube_failure        text,
    ADD COLUMN demo_youtube_attempted_at   timestamptz;
CREATE UNIQUE INDEX ux_sprint_demo_item ON sprint (demo_work_item_id) WHERE demo_work_item_id IS NOT NULL;
CREATE INDEX ix_sprint_demo_youtube_pending ON sprint (demo_youtube_attempted_at) WHERE demo_youtube_state = 'pending' AND archived_at IS NULL;

CREATE TABLE project_demo_settings (
    project_id   uuid PRIMARY KEY REFERENCES project (id),
    automatic    boolean NOT NULL DEFAULT false,
    environment  text NOT NULL DEFAULT '',
    logo_blob    text,                              -- IBlobStore key, null = ShipLean's logo; clearing it keeps the bytes
    logo_version integer NOT NULL DEFAULT 0 CHECK (logo_version >= 0),
    tokens       text NOT NULL DEFAULT '',          -- K18's normalised lines, '' = ShipLean's tokens
    updated_by   uuid REFERENCES app_user (id),
    updated_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE project_youtube_channel (
    id            uuid PRIMARY KEY,
    project_id    uuid NOT NULL REFERENCES project (id),
    status        text NOT NULL CHECK (status IN ('pending', 'connected')),
    channel_id    text NOT NULL,
    channel_title text NOT NULL,
    visibility    text CHECK (visibility IN ('public', 'unlisted', 'private')),
    refresh_token text NOT NULL,                -- Data Protection, IntegrationTokens; '' once disconnected
    refusal       text,
    checked_at    timestamptz,
    connected_by  uuid NOT NULL REFERENCES app_user (id),
    created_at    timestamptz NOT NULL DEFAULT now(),
    archived_at   timestamptz
);
CREATE UNIQUE INDEX ux_project_youtube_live ON project_youtube_channel (project_id) WHERE archived_at IS NULL;

ALTER TABLE instance_settings
    ADD COLUMN IF NOT EXISTS youtube_client_id text,
    ADD COLUMN IF NOT EXISTS youtube_client_secret text,       -- IntegrationTokens
    ADD COLUMN IF NOT EXISTS youtube_client_secret_last4 text;

ALTER TABLE transfer_token DROP CONSTRAINT transfer_token_kind_check;
ALTER TABLE transfer_token ADD CONSTRAINT transfer_token_kind_check CHECK (kind IN ('upload', 'download', 'demo', 'demoLogo'));
ALTER TABLE transfer_token ADD COLUMN revoked_at timestamptz;
