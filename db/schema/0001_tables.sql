-- The v1 schema.
--
-- Target: PostgreSQL 18.
--   * snake_case, singular table names
--   * created_at / updated_at timestamptz on every mutable table
--   * archived_at = soft delete; app code never hard-deletes
--   * enums are text + CHECK (camelCase values, the same strings the persisted JSON uses) so values can evolve
--     without ALTER TYPE
--   * ids are uuid; ordering keys (backlog_order, column_order) are text compared with COLLATE "C"
--   * persisted JSON columns (board.settings, revision.changes, import_job.progress, user_preference.json,
--     work_item.acceptance_criteria) follow the pinned shapes in src/ShipLean.TestSupport/PinnedShapes

-- The install itself: exactly one row.
CREATE TABLE instance_settings (
    id               boolean PRIMARY KEY DEFAULT true CHECK (id),
    name             text NOT NULL,
    base_url         text NOT NULL,
    project_creation text NOT NULL DEFAULT 'everyone' CHECK (project_creation IN ('everyone', 'admins')),
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);

-- A person who can sign in and be assigned work. Never deleted: disabled_at switches an account off.
CREATE TABLE app_user (
    id                    uuid PRIMARY KEY,
    email                 text NOT NULL,
    name                  text NOT NULL,
    initials              text NOT NULL CHECK (initials ~ '^[A-Z]{1,2}$'),
    avatar_colour         text NOT NULL CHECK (avatar_colour ~ '^#[0-9a-f]{6}$'),
    avatar_photo_blob     text,
    photo_version         integer NOT NULL DEFAULT 0 CHECK (photo_version >= 0), -- bumped on every photo upload, replace or remove; ?v= on photoUrl
    password_hash         text,
    must_change_password  boolean NOT NULL DEFAULT false,
    security_stamp        text NOT NULL,
    failed_sign_in_count  integer NOT NULL DEFAULT 0 CHECK (failed_sign_in_count >= 0),
    locked_until          timestamptz,
    is_admin              boolean NOT NULL DEFAULT false,
    disabled_at           timestamptz,
    last_sign_in_at       timestamptz,
    created_at            timestamptz NOT NULL DEFAULT now(),
    updated_at            timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_app_user_email ON app_user (lower(email));

-- Per-user settings: columns, toggles, filters, recent projects, theme, density, saved views.
CREATE TABLE user_preference (
    user_id    uuid NOT NULL REFERENCES app_user (id),
    key        text NOT NULL,
    json       jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, key)
);

-- ASP.NET Core Data Protection key ring, so cookies survive restarts and several instances.
CREATE TABLE data_protection_key (
    id            integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    friendly_name text NOT NULL UNIQUE,
    xml           text NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);

-- One team's backlog, boards and sprints. The key is immutable, so work item keys never change.
CREATE TABLE project (
    id                uuid PRIMARY KEY,
    key               text NOT NULL CHECK (key ~ '^[A-Z]{2,4}$'),
    name              text NOT NULL,
    description       text NOT NULL DEFAULT '',
    next_item_number  integer NOT NULL DEFAULT 1 CHECK (next_item_number >= 1),
    working_days      smallint[] NOT NULL DEFAULT '{1,2,3,4,5}' CHECK (working_days <@ ARRAY[1, 2, 3, 4, 5, 6, 7]::smallint[] AND cardinality(working_days) >= 1),
    velocity_window   integer NOT NULL DEFAULT 3 CHECK (velocity_window >= 1),
    velocity_override numeric(8, 2) CHECK (velocity_override IS NULL OR velocity_override >= 0),
    created_by        uuid REFERENCES app_user (id),
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_project_key ON project (key);

-- Who belongs to a project and whether they administer it. Removing a member sets archived_at; re-adding revives the row.
CREATE TABLE project_member (
    id          uuid PRIMARY KEY,
    project_id  uuid NOT NULL REFERENCES project (id),
    user_id     uuid NOT NULL REFERENCES app_user (id),
    role        text NOT NULL DEFAULT 'member' CHECK (role IN ('member', 'admin')),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    archived_at timestamptz,
    UNIQUE (project_id, user_id)
);
CREATE INDEX ix_project_member_user ON project_member (user_id);

-- A workflow state of one work item type in one project, with its category (the Removed category is a work item's soft
-- delete). A state deleted in project settings is archived (its items moved to a target first); its name can be reused.
CREATE TABLE state (
    id          uuid PRIMARY KEY,
    project_id  uuid NOT NULL REFERENCES project (id),
    type        text NOT NULL CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task')),
    name        text NOT NULL,
    category    text NOT NULL CHECK (category IN ('proposed', 'inProgress', 'resolved', 'completed', 'removed')),
    sort_order  integer NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    archived_at timestamptz,
    UNIQUE (id, project_id, type)
);
CREATE UNIQUE INDEX ux_state_name ON state (project_id, type, name) WHERE archived_at IS NULL;

-- A time box. Past / current / future is derived from the dates, never stored.
CREATE TABLE sprint (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project (id),
    name            text NOT NULL,
    start_date      date NOT NULL,
    end_date        date NOT NULL,
    goal            text NOT NULL DEFAULT '',
    rollover_status text CHECK (rollover_status IN ('pending', 'decided')),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    archived_at     timestamptz,
    CHECK (end_date >= start_date),
    UNIQUE (id, project_id)
);
CREATE UNIQUE INDEX ux_sprint_name ON sprint (project_id, name) WHERE archived_at IS NULL;
CREATE INDEX ix_sprint_dates ON sprint (project_id, start_date);

-- Story points a member can take in a sprint (one row per member and sprint). Clearing a member's entry archives the row;
-- entering it again revives the same row.
CREATE TABLE sprint_capacity (
    id              uuid PRIMARY KEY,
    sprint_id       uuid NOT NULL REFERENCES sprint (id),
    user_id         uuid NOT NULL REFERENCES app_user (id),
    capacity_points numeric(6, 2) NOT NULL CHECK (capacity_points >= 0),
    note            text,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    archived_at     timestamptz,
    UNIQUE (sprint_id, user_id)
);

-- The single unit of work, whatever its type. Parent type is constrained (Epic <- Feature <- Feature|Story|Bug, Story|Bug <- Task);
-- every parent is optional. Board position (board_column_id, swimlane_id) is text because columns and lanes live in board.settings.
CREATE TABLE work_item (
    id                 uuid PRIMARY KEY,
    project_id         uuid NOT NULL REFERENCES project (id),
    number             integer NOT NULL CHECK (number >= 1),
    key                text NOT NULL CHECK (key ~ '^[A-Z]{2,4}-[0-9]+$'),
    type               text NOT NULL CHECK (type IN ('epic', 'feature', 'story', 'bug', 'task')),
    title              text NOT NULL,
    description        text NOT NULL DEFAULT '',
    acceptance_criteria jsonb NOT NULL DEFAULT '[]' CHECK (jsonb_typeof(acceptance_criteria) = 'array'),
    repro_steps        text NOT NULL DEFAULT '',
    severity           smallint CHECK (severity BETWEEN 1 AND 4),
    state_id           uuid NOT NULL,
    assigned_to        uuid REFERENCES app_user (id),
    sprint_id          uuid,
    priority           smallint NOT NULL DEFAULT 2 CHECK (priority BETWEEN 1 AND 4),
    story_points       numeric(6, 2) CHECK (story_points IS NULL OR story_points >= 0),
    backlog_order      text NOT NULL COLLATE "C",
    parent_id          uuid,
    parent_type        text,
    tags               text[] NOT NULL DEFAULT '{}',
    board_column_id    text,
    board_column_done  boolean NOT NULL DEFAULT false,
    swimlane_id        text,
    column_order       text COLLATE "C",
    original_system    text,
    original_system_id text,
    original_url       text,
    revision           integer NOT NULL DEFAULT 1 CHECK (revision >= 1),
    created_by         uuid REFERENCES app_user (id),
    created_at         timestamptz NOT NULL DEFAULT now(),
    -- changed_at is the model's field and stands in for updated_at on this table.
    changed_by         uuid REFERENCES app_user (id),
    changed_at         timestamptz NOT NULL DEFAULT now(),
    state_changed_at   timestamptz NOT NULL DEFAULT now(),
    UNIQUE (project_id, number),
    UNIQUE (key),
    UNIQUE (id, type),
    FOREIGN KEY (state_id, project_id, type) REFERENCES state (id, project_id, type),
    FOREIGN KEY (sprint_id, project_id) REFERENCES sprint (id, project_id),
    FOREIGN KEY (parent_id, parent_type) REFERENCES work_item (id, type),
    CONSTRAINT ck_work_item_parent_both_or_neither CHECK ((parent_id IS NULL) = (parent_type IS NULL)),
    CONSTRAINT ck_work_item_parent_not_self CHECK (parent_id IS NULL OR parent_id <> id),
    CONSTRAINT ck_work_item_parent_type_allowed CHECK (
        parent_type IS NULL
        OR (type = 'feature' AND parent_type IN ('epic', 'feature'))
        OR (type IN ('story', 'bug') AND parent_type = 'feature')
        OR (type = 'task' AND parent_type IN ('story', 'bug'))),
    CONSTRAINT ck_work_item_original_together CHECK (
        (original_system IS NULL AND original_system_id IS NULL AND original_url IS NULL)
        OR (original_system IS NOT NULL AND original_system_id IS NOT NULL))
);
CREATE INDEX ix_work_item_backlog ON work_item (project_id, type, backlog_order);
CREATE INDEX ix_work_item_sprint ON work_item (sprint_id) WHERE sprint_id IS NOT NULL;
CREATE INDEX ix_work_item_parent ON work_item (parent_id) WHERE parent_id IS NOT NULL;
CREATE INDEX ix_work_item_board ON work_item (project_id, board_column_id, board_column_done, column_order);
CREATE INDEX ix_work_item_assignee ON work_item (assigned_to) WHERE assigned_to IS NOT NULL;
CREATE INDEX ix_work_item_state ON work_item (state_id);
CREATE UNIQUE INDEX ux_work_item_original ON work_item (project_id, original_system, original_system_id) WHERE original_system IS NOT NULL;

-- A link that isn't parent/child: between two items, or to a URL (item links set target_id, URL links set url).
CREATE TABLE work_item_link (
    id          uuid PRIMARY KEY,
    source_id   uuid NOT NULL REFERENCES work_item (id),
    target_id   uuid REFERENCES work_item (id),
    link_type   text NOT NULL CHECK (link_type IN ('predecessor', 'successor', 'blockedBy', 'related', 'duplicate', 'url')),
    url         text,
    title       text,
    created_by  uuid REFERENCES app_user (id),
    created_at  timestamptz NOT NULL DEFAULT now(),
    archived_at timestamptz,
    CHECK ((link_type = 'url') = (target_id IS NULL)),
    CHECK ((link_type = 'url') = (url IS NOT NULL)),
    CHECK (target_id IS NULL OR target_id <> source_id)
);
CREATE INDEX ix_work_item_link_source ON work_item_link (source_id);
CREATE INDEX ix_work_item_link_target ON work_item_link (target_id) WHERE target_id IS NOT NULL;

-- One board per project and level; settings holds columns, swimlanes, card fields, style rules, tag colours and annotations.
CREATE TABLE board (
    id         uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id),
    level      text NOT NULL CHECK (level IN ('stories', 'features', 'epics', 'taskboard')),
    settings   jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (project_id, level)
);

-- Discussion on a work item (markdown).
CREATE TABLE comment (
    id           uuid PRIMARY KEY,
    work_item_id uuid NOT NULL REFERENCES work_item (id),
    author_id    uuid REFERENCES app_user (id),
    body         text NOT NULL,
    original_system    text,
    original_system_id text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    archived_at  timestamptz,
    CHECK ((original_system IS NULL) = (original_system_id IS NULL))
);
CREATE INDEX ix_comment_work_item ON comment (work_item_id, created_at);
-- A re-run import updates the comment it wrote before instead of adding a second one.
CREATE UNIQUE INDEX ux_comment_original ON comment (work_item_id, original_system, original_system_id) WHERE original_system IS NOT NULL;

-- A file on a work item; the bytes live in the blob store under storage_ref.
CREATE TABLE attachment (
    id           uuid PRIMARY KEY,
    work_item_id uuid NOT NULL REFERENCES work_item (id),
    name         text NOT NULL,
    content_type text NOT NULL,
    size_bytes   bigint NOT NULL CHECK (size_bytes >= 0),
    storage_ref  text NOT NULL,
    uploaded_by  uuid REFERENCES app_user (id),
    original_system    text,
    original_system_id text,
    created_at   timestamptz NOT NULL DEFAULT now(),
    archived_at  timestamptz,
    CHECK ((original_system IS NULL) = (original_system_id IS NULL))
);
CREATE INDEX ix_attachment_work_item ON attachment (work_item_id);
-- A re-run import updates the attachment it wrote before instead of adding a second one.
CREATE UNIQUE INDEX ux_attachment_original ON attachment (work_item_id, original_system, original_system_id) WHERE original_system IS NOT NULL;

-- A Jira project import into one ShipLean project: status, progress document and timestamps. Re-runnable.
CREATE TABLE import_job (
    id               uuid PRIMARY KEY,
    connection_id    uuid NOT NULL,
    jira_project_key text NOT NULL,
    project_key      text NOT NULL CHECK (project_key ~ '^[A-Z]{2,4}$'),
    project_id       uuid REFERENCES project (id),
    status           text NOT NULL CHECK (status IN ('queued', 'running', 'done', 'doneWithErrors', 'stopped', 'failed')),
    progress         jsonb NOT NULL,
    started_at       timestamptz,
    finished_at      timestamptz,
    created_by       uuid REFERENCES app_user (id),
    created_at       timestamptz NOT NULL DEFAULT now(),
    updated_at       timestamptz NOT NULL DEFAULT now()
);

-- The field-change audit trail, one row per save. changed_by null = the import itself. Velocity and burndown read it.
CREATE TABLE revision (
    work_item_id    uuid NOT NULL REFERENCES work_item (id),
    rev             integer NOT NULL CHECK (rev >= 1),
    changed_by      uuid REFERENCES app_user (id),
    changed_at      timestamptz NOT NULL,
    original_system text,
    import_job_id   uuid REFERENCES import_job (id),
    changes         jsonb NOT NULL CHECK (jsonb_typeof(changes) = 'array'),
    PRIMARY KEY (work_item_id, rev)
);
CREATE INDEX ix_revision_changed_at ON revision (changed_at);

-- Credentials for a Jira site, owned by a project's admins. The API token is stored encrypted (null until set).
CREATE TABLE jira_connection (
    id                  uuid PRIMARY KEY,
    project_id          uuid NOT NULL REFERENCES project (id),
    base_url            text NOT NULL,
    email               text NOT NULL,
    api_token_encrypted text,
    created_by          uuid REFERENCES app_user (id),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    archived_at         timestamptz
);

ALTER TABLE import_job ADD CONSTRAINT fk_import_job_connection FOREIGN KEY (connection_id) REFERENCES jira_connection (id);
CREATE INDEX ix_import_job_project ON import_job (project_id);

-- How a Jira concept lands in ShipLean (issue type, status, user, field), reviewed before the run.
CREATE TABLE import_mapping (
    id            uuid PRIMARY KEY,
    import_job_id uuid NOT NULL REFERENCES import_job (id),
    kind          text NOT NULL CHECK (kind IN ('issueType', 'status', 'user', 'field')),
    source_value  text NOT NULL,
    target        jsonb NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    UNIQUE (import_job_id, kind, source_value)
);

-- An item the import could not write; resolved by retrying or skipping it.
CREATE TABLE import_error (
    id            uuid PRIMARY KEY,
    import_job_id uuid NOT NULL REFERENCES import_job (id),
    step          text NOT NULL,
    entity        text NOT NULL,
    source_key    text NOT NULL,
    message       text NOT NULL,
    resolution    text NOT NULL DEFAULT 'pending' CHECK (resolution IN ('pending', 'retried', 'skipped')),
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_import_error_job ON import_error (import_job_id);

-- The import's running log, newest last.
CREATE TABLE import_log (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    import_job_id uuid NOT NULL REFERENCES import_job (id),
    logged_at     timestamptz NOT NULL DEFAULT now(),
    level         text NOT NULL DEFAULT 'info' CHECK (level IN ('info', 'warning', 'error')),
    message       text NOT NULL
);
CREATE INDEX ix_import_log_job ON import_log (import_job_id, id);
