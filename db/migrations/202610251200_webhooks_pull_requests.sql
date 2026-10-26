-- GitHub and GitLab webhooks (#39): pull and merge request events update the state of the links to them, link them to
-- the Stories, Tasks and Bugs whose keys they carry, and close an item once its pull requests have merged.

-- The comparable form of a link URL, so a stored link and a provider's event URL for the same pull or merge request are
-- equal: no scheme, query or fragment, GitLab's "/-/" dropped, anything after a pull or merge request's number dropped
-- ("/files", "/diffs"), no trailing slash, lower case.
CREATE FUNCTION link_url_key(url text) RETURNS text
    LANGUAGE sql IMMUTABLE PARALLEL SAFE RETURNS NULL ON NULL INPUT
    RETURN lower(regexp_replace(
        regexp_replace(
            replace(regexp_replace(regexp_replace(btrim(url), '^[a-z][a-z0-9+.-]*://', '', 'i'), '[?#].*', ''), '/-/', '/'),
            '^(.*/(pull|merge_requests)/[0-9]+)(/.*)?$', '\1'),
        '/+$', ''));

CREATE INDEX ix_work_item_link_url_key ON work_item_link (link_url_key(url)) WHERE link_type = 'url';

-- A link a webhook added because the pull or merge request named the item's key: which provider's webhook, and whether
-- the key was in the title or the source branch. Null for a link a person or an agent added.
ALTER TABLE work_item_link
    ADD COLUMN added_via  text CHECK (added_via IN ('github', 'gitlab')),
    ADD COLUMN matched_on text CHECK (matched_on IN ('title', 'branch')),
    ADD CONSTRAINT ck_work_item_link_added_via_together CHECK ((added_via IS NULL) = (matched_on IS NULL));

-- The last state a provider reported for a pull or merge request, keyed by link_url_key, so every link to it shows the
-- same card; source is the provider whose webhook reported it. A report older than as_of is ignored, so a late or
-- replayed delivery can't turn a merged card back to open.
CREATE TABLE link_state (
    url_key    text PRIMARY KEY,
    url        text NOT NULL,
    state      text NOT NULL CHECK (state IN ('open', 'merged', 'closed')),
    title      text,
    as_of      timestamptz NOT NULL,
    source     text NOT NULL CHECK (source IN ('github', 'gitlab')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

-- Every webhook delivery that passed the signature check, by the provider's delivery id: a second delivery with the same
-- id is a no-op, and the row is the audit trail of which repository and pull or merge request it was.
CREATE TABLE webhook_delivery (
    provider    text NOT NULL CHECK (provider IN ('github', 'gitlab')),
    delivery_id text NOT NULL,
    event       text NOT NULL,
    repository  text,
    url         text,
    received_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (provider, delivery_id)
);

-- The pull or merge request a webhook write came from, as "GitHub acme/api#12" or "GitLab acme/api!7". changed_by is
-- null on such a revision: no person made it.
ALTER TABLE revision ADD COLUMN webhook text;
