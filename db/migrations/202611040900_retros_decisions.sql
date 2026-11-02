-- Retro decisions (SHIP-497, contract K1). Additive only. An action item covers many points through retro_action_point; a point is
-- Not needed through retro_point.not_needed_reason. retro_action.point_id and ux_retro_action_point stay, but nothing new reads or writes point_id.
-- The live link is a join row with archived_at IS NULL whose work item's state category is not 'removed'.
CREATE TABLE retro_action_point (
    work_item_id uuid NOT NULL REFERENCES retro_action (work_item_id),
    point_id     uuid NOT NULL REFERENCES retro_point (id),
    position     integer NOT NULL DEFAULT 0,
    created_at   timestamptz NOT NULL DEFAULT now(),
    archived_at  timestamptz,
    PRIMARY KEY (work_item_id, point_id)
);
CREATE UNIQUE INDEX ux_retro_action_point_live ON retro_action_point (point_id) WHERE archived_at IS NULL;
CREATE INDEX ix_retro_action_point_action ON retro_action_point (work_item_id);
INSERT INTO retro_action_point (work_item_id, point_id, created_at)
    SELECT work_item_id, point_id, created_at FROM retro_action WHERE point_id IS NOT NULL;

-- Null on back-copied rows.
ALTER TABLE retro_action ADD COLUMN fix_type text CHECK (fix_type IN ('skill', 'guide', 'test', 'analyzer', 'product', 'other'));
ALTER TABLE retro_point ADD COLUMN not_needed_reason text, ADD COLUMN decided_by_user_id uuid REFERENCES app_user (id),
    ADD COLUMN decided_agent_name text, ADD COLUMN decided_at timestamptz;
ALTER TABLE retro_point ADD CONSTRAINT ck_retro_point_not_needed CHECK (not_needed_reason IS NULL OR category <> 'wentWell');

CREATE TABLE retro_theme_merge_proposal (
    id                  uuid PRIMARY KEY,
    project_id          uuid NOT NULL REFERENCES project (id),
    from_theme_id       uuid NOT NULL REFERENCES retro_theme (id),
    into_theme_id       uuid NOT NULL REFERENCES retro_theme (id),
    reason              text NOT NULL,
    status              text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'merged', 'keptApart')),
    proposed_by_user_id uuid NOT NULL REFERENCES app_user (id),
    proposed_agent_name text,
    decided_by_user_id  uuid REFERENCES app_user (id),
    decided_at          timestamptz,
    created_at          timestamptz NOT NULL DEFAULT now(),
    archived_at         timestamptz,
    CONSTRAINT ck_merge_two_themes CHECK (from_theme_id <> into_theme_id)
);
CREATE UNIQUE INDEX ux_retro_merge_pair ON retro_theme_merge_proposal (LEAST(from_theme_id, into_theme_id), GREATEST(from_theme_id, into_theme_id)) WHERE archived_at IS NULL;
