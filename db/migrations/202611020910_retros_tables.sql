-- Retrospectives (SHIP-82, contract C2). Every table soft-deletes through archived_at, except retro_failure_log, which is never changed.
-- retrospective.sprint_id is the only thing that ties a retro (and its points) to a sprint; null means "no sprint".
-- The tsv columns follow search_doc: english for stemming and simple so identifiers match exactly; stored so GIN can index them.
CREATE TABLE retro_theme (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project (id),
    name            text NOT NULL,
    name_set_by     text NOT NULL CHECK (name_set_by IN ('auto', 'agent', 'person')),
    tone            text NOT NULL CHECK (tone IN ('good', 'bad')),
    centroid        real[],
    centroid_model  text,
    point_count     integer NOT NULL DEFAULT 0,
    merged_into_id  uuid REFERENCES retro_theme (id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    archived_at     timestamptz
);
CREATE UNIQUE INDEX ux_retro_theme_name ON retro_theme (project_id, lower(name)) WHERE archived_at IS NULL AND merged_into_id IS NULL;

CREATE TABLE retrospective (
    id              uuid PRIMARY KEY,
    project_id      uuid NOT NULL REFERENCES project (id),
    work_item_id    uuid REFERENCES work_item (id),
    sprint_id       uuid REFERENCES sprint (id),
    author_user_id  uuid NOT NULL REFERENCES app_user (id),
    agent_name      text,
    role            text NOT NULL CHECK (role IN ('conductor', 'subAgent', 'person')),
    partial         boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    archived_at     timestamptz,
    CONSTRAINT ck_retrospective_target CHECK (work_item_id IS NOT NULL OR sprint_id IS NOT NULL)
);
CREATE UNIQUE INDEX ux_retrospective_item_agent ON retrospective (work_item_id, agent_name) WHERE agent_name IS NOT NULL AND archived_at IS NULL;
CREATE INDEX ix_retrospective_project ON retrospective (project_id);
CREATE INDEX ix_retrospective_sprint ON retrospective (sprint_id);
CREATE INDEX ix_retrospective_work_item ON retrospective (work_item_id);

CREATE TABLE retrospective_source (
    retrospective_id        uuid NOT NULL REFERENCES retrospective (id),
    source_retrospective_id uuid NOT NULL REFERENCES retrospective (id),
    PRIMARY KEY (retrospective_id, source_retrospective_id)
);

CREATE TABLE retro_point (
    id                 uuid PRIMARY KEY,
    retrospective_id   uuid NOT NULL REFERENCES retrospective (id),
    category           text NOT NULL CHECK (category IN ('wentWell', 'wentBadly', 'improve', 'notInPlan')),
    text               text NOT NULL,
    root_cause         text,
    theme_id           uuid REFERENCES retro_theme (id),
    theme_set_by       text CHECK (theme_set_by IN ('auto', 'agent', 'person')),
    embedding          real[],
    created_by_user_id uuid NOT NULL REFERENCES app_user (id),
    agent_name         text,
    position           integer NOT NULL DEFAULT 0,
    created_at         timestamptz NOT NULL DEFAULT now(),
    archived_at        timestamptz,
    tsv                tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english'::regconfig, text), 'A'::"char")
        || setweight(to_tsvector('english'::regconfig, coalesce(root_cause, '')), 'B'::"char")
        || setweight(to_tsvector('simple'::regconfig, text), 'A'::"char")
        || setweight(to_tsvector('simple'::regconfig, coalesce(root_cause, '')), 'B'::"char")
    ) STORED,
    CONSTRAINT ck_retro_point_root_cause CHECK (root_cause IS NULL OR category = 'wentBadly')
);
CREATE INDEX ix_retro_point_tsv ON retro_point USING gin (tsv);
CREATE INDEX ix_retro_point_retrospective ON retro_point (retrospective_id);
CREATE INDEX ix_retro_point_theme ON retro_point (theme_id);

CREATE TABLE retro_failure_log (
    id               uuid PRIMARY KEY,
    retrospective_id uuid NOT NULL REFERENCES retrospective (id),
    kind             text NOT NULL CHECK (kind IN ('blocker', 'assumptionDisproved', 'friction')),
    text             text NOT NULL,
    position         integer NOT NULL DEFAULT 0,
    created_at       timestamptz NOT NULL DEFAULT now(),
    tsv              tsvector GENERATED ALWAYS AS (
        to_tsvector('english'::regconfig, text) || to_tsvector('simple'::regconfig, text)
    ) STORED
);
CREATE INDEX ix_retro_failure_log_tsv ON retro_failure_log USING gin (tsv);
CREATE INDEX ix_retro_failure_log_retrospective ON retro_failure_log (retrospective_id);

CREATE TABLE retro_action (
    work_item_id     uuid PRIMARY KEY REFERENCES work_item (id),
    project_id       uuid NOT NULL REFERENCES project (id),
    theme_id         uuid REFERENCES retro_theme (id),
    point_id         uuid REFERENCES retro_point (id),
    raised_sprint_id uuid REFERENCES sprint (id),
    created_at       timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_retro_action_theme ON retro_action (theme_id);
CREATE INDEX ix_retro_action_project ON retro_action (project_id);
CREATE UNIQUE INDEX ux_retro_action_point ON retro_action (point_id) WHERE point_id IS NOT NULL;
