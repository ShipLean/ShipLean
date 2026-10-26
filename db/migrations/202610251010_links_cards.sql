-- URL links become cards: a note, the kind of address, what the last preview read, and the live or reported state. All
-- columns are nullable (a link written by the story form has none of them, and migrations run before the seed). The
-- title column keeps meaning "typed by a person"; the fetched title is preview_title.
ALTER TABLE work_item_link
    ADD COLUMN note                text,
    ADD COLUMN kind                text CHECK (kind IN ('githubPull', 'githubIssue', 'gitlabMergeRequest', 'gitlabIssue', 'page')),
    ADD COLUMN preview_title       text,
    ADD COLUMN preview_description text,
    ADD COLUMN preview_site        text,
    ADD COLUMN preview_image_key   text,
    ADD COLUMN preview_favicon_key text,
    ADD COLUMN preview_status      text CHECK (preview_status IN ('ok', 'unreachable', 'private', 'none')),
    ADD COLUMN preview_fetched_at  timestamptz,
    ADD COLUMN number              integer,
    ADD COLUMN location            text,
    ADD COLUMN author              text,
    ADD COLUMN state               text CHECK (state IN ('open', 'draft', 'merged', 'closed')),
    ADD COLUMN state_source        text CHECK (state_source IN ('live', 'reported', 'none')),
    ADD COLUMN state_as_of         timestamptz,
    ADD COLUMN state_reported_by   text;

-- A reported state is found by address, across projects.
CREATE INDEX ix_work_item_link_url ON work_item_link (url) WHERE url IS NOT NULL AND archived_at IS NULL;
