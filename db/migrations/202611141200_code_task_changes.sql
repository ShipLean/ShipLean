-- Task changes (SHIP-1460): the commits a Task or Bug was done in, what each commit is, and code comments on their lines.
-- Additive only; nothing here is ever deleted: rows are archived (archived_at). Sorts after 202611101400_phone_screens.sql.
-- There is no table for Viewed: it is kept in the browser.

-- A repository on GitHub or GitLab, instance-wide, made the first time a commit names it.
CREATE TABLE code_repository (
    id         uuid PRIMARY KEY,
    provider   text NOT NULL CHECK (provider IN ('github', 'gitlab')),
    host       text NOT NULL,   -- github.com, gitlab.example.com (lower case)
    path       text NOT NULL,   -- acme/storefront (lower case, no .git)
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (provider, host, path)
);

-- A commit on a Task or Bug, in order. 'migration' is a link card the one-off startup job moved here.
CREATE TABLE work_item_commit (
    id             uuid PRIMARY KEY,
    work_item_id   uuid NOT NULL REFERENCES work_item (id),
    repository_id  uuid NOT NULL REFERENCES code_repository (id),
    sha            text NOT NULL CHECK (sha ~ '^([0-9a-f]{40}|[0-9a-f]{64})$'),
    position       int  NOT NULL,
    added_via      text NOT NULL CHECK (added_via IN ('report_task', 'add_commits', 'form', 'migration')),
    added_by       uuid REFERENCES app_user (id),
    added_by_agent text,
    created_at     timestamptz NOT NULL DEFAULT now(),
    archived_at    timestamptz
);
CREATE UNIQUE INDEX ux_work_item_commit_live ON work_item_commit (work_item_id, repository_id, sha) WHERE archived_at IS NULL;
CREATE INDEX ix_work_item_commit_position ON work_item_commit (work_item_id, position);

-- What a commit is, read once when it is added. No patch is stored: the diff is fetched live and cached for a day.
-- paths holds the files the commit touches, so section totals and "files viewed" never fetch patches.
CREATE TABLE code_commit (
    repository_id uuid NOT NULL REFERENCES code_repository (id),
    sha           text NOT NULL,
    parent_shas   text[] NOT NULL,
    subject       text NOT NULL,
    author_name   text NOT NULL,
    authored_at   timestamptz NOT NULL,
    additions     int NOT NULL,
    deletions     int NOT NULL,
    files_changed int NOT NULL,
    paths         text[] NOT NULL,
    read_at       timestamptz NOT NULL,
    PRIMARY KEY (repository_id, sha)
);

-- A comment on a line, or a reply to one. A top-level comment is anchored by repository + sha + path + side + line;
-- a reply has only parent_id. body_html is rendered once by MarkdownRenderer.
CREATE TABLE code_comment (
    id            uuid PRIMARY KEY,
    work_item_id  uuid NOT NULL REFERENCES work_item (id),
    parent_id     uuid REFERENCES code_comment (id),
    repository_id uuid REFERENCES code_repository (id),
    sha           text,            -- the newest commit on screen when it was written
    path          text,
    side          text CHECK (side IN ('old', 'new')),
    line          int,
    start_line    int,             -- a range
    line_text     text,            -- the line as it was, for an outdated comment
    body          text NOT NULL,
    body_html     text NOT NULL,
    author_id     uuid REFERENCES app_user (id),
    author_agent  text,
    created_at    timestamptz NOT NULL DEFAULT now(),
    resolved_at   timestamptz,
    resolved_by   uuid REFERENCES app_user (id),
    outdated_at   timestamptz,     -- a later commit changed the line
    sent_back_at  timestamptz,     -- went back with Request changes
    archived_at   timestamptz,
    CONSTRAINT ck_code_comment_anchor CHECK (
        parent_id IS NOT NULL
        OR (repository_id IS NOT NULL AND sha IS NOT NULL AND path IS NOT NULL AND side IS NOT NULL AND line IS NOT NULL))
);
CREATE INDEX ix_code_comment_work_item ON code_comment (work_item_id) WHERE archived_at IS NULL;
