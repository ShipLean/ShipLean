-- The phone app's screens (SHIP-916 contract N1): a Feature's health, +1 support on a retro point, and when a person last saw the
-- Feature digest. Additive only, and nothing here is ever deleted: a support is withdrawn by its timestamp, and a seen time is
-- overwritten. Sorts after 202611101300_inbox_mentions.sql.

-- A Feature's health, set by a person or an agent. Only Features carry it; ck_work_item_feature_fields_feature_only is left alone.
ALTER TABLE work_item
    ADD COLUMN health           text CHECK (health IN ('onTrack', 'atRisk', 'blocked')),
    ADD COLUMN health_set_at    timestamptz,
    ADD COLUMN health_set_by    uuid REFERENCES app_user (id),
    ADD COLUMN health_set_agent text,
    ADD CONSTRAINT ck_work_item_health_feature_only CHECK (
        type = 'feature' OR (health IS NULL AND health_set_at IS NULL AND health_set_by IS NULL AND health_set_agent IS NULL));

-- A person's +1 on a retro point. Withdrawing sets withdrawn_at; supporting again adds a new row, so only one row per person is live.
CREATE TABLE retro_point_support (
    id           uuid PRIMARY KEY,
    point_id     uuid NOT NULL REFERENCES retro_point (id),
    user_id      uuid NOT NULL REFERENCES app_user (id),
    created_at   timestamptz NOT NULL,
    withdrawn_at timestamptz
);
CREATE UNIQUE INDEX ux_retro_point_support_live ON retro_point_support (point_id, user_id) WHERE withdrawn_at IS NULL;

-- When a person last saw a project's Feature digest; one row per person and project, upserted.
CREATE TABLE feature_digest_seen (
    user_id    uuid NOT NULL REFERENCES app_user (id),
    project_id uuid NOT NULL REFERENCES project (id),
    seen_at    timestamptz NOT NULL,
    PRIMARY KEY (user_id, project_id)
);
