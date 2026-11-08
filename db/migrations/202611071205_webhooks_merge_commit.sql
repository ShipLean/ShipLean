-- GitHub issue conversations (SHIP-742, D14): a merged pull request's merge commit and when it merged, so "fixed in"
-- names the commit that completed the item. Null until a merge is reported, and for GitLab.
ALTER TABLE link_state
    ADD COLUMN merge_commit_sha text CHECK (merge_commit_sha ~ '^[0-9a-f]{7,64}$'),
    ADD COLUMN merged_at        timestamptz;
