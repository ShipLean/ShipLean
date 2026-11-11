-- Scheduled backups (SHIP-743, contract C-RUN). backup_settings is one row (id 1), written only by the Backups tab; no row
-- means the instance was never configured. The secrets (destination_secret, passphrase) are Data Protection ciphertext.
-- backup_run is the run log: rows are never deleted (pruning sets pruned_at, an abandoned run is updated to failed), and
-- backup_run_occurrence lets each scheduled occurrence run, or be recorded as skipped, once across every instance.
CREATE TABLE backup_settings (
    id                    smallint PRIMARY KEY CHECK (id = 1),
    enabled               boolean NOT NULL DEFAULT false,
    provider              text CHECK (provider IN ('s3')),
    destination           jsonb,                 -- the destination without its secret key
    destination_secret    text,                  -- Data Protection, ShipLean.Instance.Backups.Secret.v1
    secret_last4          text,
    cron                  text NOT NULL DEFAULT '0 2 * * *',
    keep_count            integer NOT NULL DEFAULT 14 CHECK (keep_count BETWEEN 1 AND 365),
    include_attachments   boolean NOT NULL DEFAULT false,
    passphrase            text,                  -- Data Protection, ShipLean.Instance.Backups.Secret.v1
    passphrase_generation integer NOT NULL DEFAULT 0 CHECK (passphrase_generation >= 0),
    passphrase_set_at     timestamptz,
    updated_at            timestamptz NOT NULL,
    updated_by            uuid REFERENCES app_user (id)
);

CREATE TABLE backup_run (
    id                    uuid PRIMARY KEY,
    trigger               text NOT NULL CHECK (trigger IN ('scheduled', 'manual')),
    state                 text NOT NULL CHECK (state IN ('running', 'succeeded', 'failed', 'skipped')),
    scheduled_for         timestamptz,
    started_at            timestamptz NOT NULL,
    finished_at           timestamptz,
    started_by            uuid REFERENCES app_user (id),
    passphrase_generation integer NOT NULL DEFAULT 0,
    database_object_key   text,
    database_bytes        bigint,
    attachments_copied    integer,
    attachments_bytes     bigint,
    error                 text,
    pruned_at             timestamptz,
    host                  text NOT NULL DEFAULT '',
    destination_id        text,                  -- BackupDestinationId of the run's snapshot; null on a skipped row
    CONSTRAINT ck_backup_run_scheduled_for CHECK ((trigger = 'scheduled') = (scheduled_for IS NOT NULL)),
    CONSTRAINT ck_backup_run_skipped CHECK (state <> 'skipped' OR trigger = 'scheduled'),
    CONSTRAINT ck_backup_run_destination CHECK (state = 'skipped' OR destination_id IS NOT NULL)
);
CREATE UNIQUE INDEX backup_run_occurrence ON backup_run (scheduled_for) WHERE trigger = 'scheduled';
CREATE INDEX ix_backup_run_started ON backup_run (started_at DESC);
CREATE INDEX ix_backup_run_unpruned ON backup_run (destination_id, started_at DESC) WHERE state = 'succeeded' AND pruned_at IS NULL;
