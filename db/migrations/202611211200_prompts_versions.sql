-- Prompts (SHIP-744, C7): the built-in prompts ShipLean has run with, a project's custom versions of them, the version in use
-- and a log of what each agent was served. Nothing here is ever deleted or archived (D17): a version stays forever, and
-- "Use built-in" only moves project_prompt.in_use_version_id.
CREATE TABLE prompt_built_in (
    prompt        text NOT NULL CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'sprintDemo')),
    hash          text NOT NULL,
    text          text NOT NULL,
    first_seen_at timestamptz NOT NULL,
    PRIMARY KEY (prompt, hash)
);

CREATE TABLE prompt_version (
    id         uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id),
    prompt     text NOT NULL CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'sprintDemo')),
    number     integer NOT NULL CHECK (number >= 1),
    text       text NOT NULL CHECK (octet_length(text) <= 65536),
    base_hash  text NOT NULL,
    note       text NOT NULL,
    source     text NOT NULL CHECK (source IN ('editor', 'builtInUpdate', 'migrated')),
    created_by uuid REFERENCES app_user (id),
    created_at timestamptz NOT NULL,
    UNIQUE (project_id, prompt, number),
    UNIQUE (id, project_id, prompt),
    FOREIGN KEY (prompt, base_hash) REFERENCES prompt_built_in (prompt, hash)
);

-- The pointer names a version of the same project and prompt, never another's (composite key).
CREATE TABLE project_prompt (
    project_id         uuid NOT NULL REFERENCES project (id),
    prompt             text NOT NULL CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'sprintDemo')),
    in_use_version_id  uuid,
    kept_built_in_hash text,
    updated_by         uuid REFERENCES app_user (id),
    updated_at         timestamptz NOT NULL,
    PRIMARY KEY (project_id, prompt),
    FOREIGN KEY (in_use_version_id, project_id, prompt) REFERENCES prompt_version (id, project_id, prompt)
);

CREATE TABLE prompt_serving (
    id            uuid PRIMARY KEY,
    project_id    uuid NOT NULL REFERENCES project (id),
    prompt        text NOT NULL CHECK (prompt IN ('issueProposal', 'work', 'guide', 'planReviewer', 'lead', 'brief', 'report', 'codeReviewer', 'fix', 'retro', 'sprintDemo')),
    version       integer,
    version_id    uuid,
    built_in_hash text NOT NULL,
    agent         text NOT NULL CHECK (agent IN ('claude-code', 'codex-cli', 'cursor', 'other')),
    client_name   text,
    client_source text,
    item_key      text,
    user_id       uuid REFERENCES app_user (id),
    served_at     timestamptz NOT NULL,
    FOREIGN KEY (version_id, project_id, prompt) REFERENCES prompt_version (id, project_id, prompt)
);
CREATE INDEX ix_prompt_serving_item ON prompt_serving (project_id, item_key, served_at);
