-- GitHub issue conversations (SHIP-742, SHIP-763): a pending progress close that the item's reopening cancelled before delivery
-- finishes unsent as 'skipped: item reopened'. Widens the last_result check; every earlier value stays allowed.
ALTER TABLE github_outbox DROP CONSTRAINT github_outbox_last_result_check;
ALTER TABLE github_outbox ADD CONSTRAINT github_outbox_last_result_check
    CHECK (last_result IN ('sending', 'ok', 'retry', 'ambiguous', 'failed', 'skipped: issue closed', 'skipped: item reopened'));
