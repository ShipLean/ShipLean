-- Aggregate retro analysis (SHIP-2161, C4 + C5 + C6): the tables an analysis is stored in, the two new fix types and the
-- retroAnalysis prompt key. Built-in prompt rows are written lazily by DbPromptWrites, so nothing is inserted into prompt_built_in.
CREATE TABLE retro_analysis (
    id                 uuid PRIMARY KEY,
    project_id         uuid NOT NULL REFERENCES project (id),
    scope_kind         text NOT NULL CHECK (scope_kind IN ('feature', 'sprint', 'range')),
    feature_id         uuid REFERENCES work_item (id),
    sprint_id          uuid REFERENCES sprint (id),
    range_from         date,
    range_to           date,
    created_by_user_id uuid NOT NULL REFERENCES app_user (id),
    agent_name         text NOT NULL,
    embedding_model    text NOT NULL,
    rewrite_model      text,
    report_markdown    text NOT NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    archived_at        timestamptz
);
CREATE INDEX ix_retro_analysis_project ON retro_analysis (project_id, created_at DESC);

CREATE TABLE retro_pattern (
    id                    uuid PRIMARY KEY,
    analysis_id           uuid NOT NULL REFERENCES retro_analysis (id),
    ref                   text NOT NULL,
    kind                  text NOT NULL CHECK (kind IN ('problem', 'wentWell')),
    title                 text NOT NULL,
    lesson                text NOT NULL,
    asks                  text[] NOT NULL DEFAULT '{}',
    centroid              real[],
    continues_pattern_id  uuid REFERENCES retro_pattern (id),
    rank                  integer NOT NULL,
    UNIQUE (analysis_id, ref)
);
CREATE INDEX ix_retro_pattern_analysis ON retro_pattern (analysis_id);

CREATE TABLE retro_analysis_point (
    analysis_id          uuid NOT NULL REFERENCES retro_analysis (id),
    point_id             uuid NOT NULL REFERENCES retro_point (id),
    outcome              text NOT NULL CHECK (outcome IN ('noise', 'folded', 'oneOff', 'clustered', 'unclustered')),
    folded_into_point_id uuid REFERENCES retro_point (id),
    generic_text         text,
    pattern_id           uuid REFERENCES retro_pattern (id),
    PRIMARY KEY (analysis_id, point_id)
);
CREATE INDEX ix_retro_analysis_point_point ON retro_analysis_point (point_id);

CREATE TABLE retro_pattern_fix (
    id                  uuid PRIMARY KEY,
    pattern_id          uuid NOT NULL REFERENCES retro_pattern (id),
    title               text NOT NULL,
    fix_type            text NOT NULL CHECK (fix_type IN ('fix', 'structure', 'analyzer', 'test', 'skill', 'guide', 'product', 'other')),
    enforcement         text NOT NULL CHECK (enforcement IN ('notCaught', 'partly', 'fully', 'discarded')),
    enforced_by         text,
    reason              text NOT NULL,
    action_work_item_id uuid REFERENCES work_item (id),
    rank                integer NOT NULL
);
CREATE INDEX ix_retro_pattern_fix_pattern ON retro_pattern_fix (pattern_id);

ALTER TABLE retro_action DROP CONSTRAINT retro_action_fix_type_check;
ALTER TABLE retro_action ADD CONSTRAINT retro_action_fix_type_check
    CHECK (fix_type IN ('fix', 'structure', 'skill', 'guide', 'test', 'analyzer', 'product', 'other'));

ALTER TABLE prompt_built_in DROP CONSTRAINT prompt_built_in_prompt_check;
ALTER TABLE prompt_built_in ADD CONSTRAINT prompt_built_in_prompt_check
    CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'retroAnalysis', 'sprintDemo'));
ALTER TABLE prompt_version DROP CONSTRAINT prompt_version_prompt_check;
ALTER TABLE prompt_version ADD CONSTRAINT prompt_version_prompt_check
    CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'retroAnalysis', 'sprintDemo'));
ALTER TABLE project_prompt DROP CONSTRAINT project_prompt_prompt_check;
ALTER TABLE project_prompt ADD CONSTRAINT project_prompt_prompt_check
    CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'retroAnalysis', 'sprintDemo'));
ALTER TABLE prompt_serving DROP CONSTRAINT prompt_serving_prompt_check;
ALTER TABLE prompt_serving ADD CONSTRAINT prompt_serving_prompt_check
    CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'retroAnalysis', 'sprintDemo'));
