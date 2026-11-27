-- The commit card migration (SHIP-1460) stamps a card it has a final answer for, so it is not read from GitHub again on every
-- start: moved cards are archived, and a card whose repository is private or gone, or whose sha is not a commit, is stamped here
-- and stays a link card. A rate limit or a network failure leaves it null, so the next run tries again.
ALTER TABLE work_item_link ADD COLUMN commit_migration_checked_at timestamptz;
