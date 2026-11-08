-- GitHub issue conversations (SHIP-742, contract C0). A project takes issues from one GitHub repository; each issue it takes
-- is a github_issue row tied to its Triage item. Multi-step work is an issue_job, every GitHub write is a github_outbox
-- row, and github_issue_effect is the ledger that makes each step's create happen once. Nothing is deleted.

-- Intake states (D7): the Triage states, which the backlogs leave out unless the state filter names them.
ALTER TABLE state ADD COLUMN is_intake boolean NOT NULL DEFAULT false;

-- One project's intake settings (D5, D8, D10, D11). repository is "owner/name" on github.com, stored when intake is turned
-- on; at most one enabled project per repository, compared without case as GitHub does.
CREATE TABLE project_issue_intake (
    project_id            uuid PRIMARY KEY REFERENCES project (id),
    enabled               boolean NOT NULL DEFAULT false,
    repository            text,
    run_as_user_id        uuid REFERENCES app_user (id),
    proposal_instructions text NOT NULL DEFAULT '',
    max_rounds            integer NOT NULL DEFAULT 5 CHECK (max_rounds >= 1),
    updated_by            uuid REFERENCES app_user (id),
    updated_at            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_project_issue_intake_repository CHECK (NOT enabled OR repository IS NOT NULL)
);
CREATE UNIQUE INDEX ux_project_issue_intake_repository ON project_issue_intake (lower(repository)) WHERE enabled;

-- An issue a project took. work_item_id is its Triage item, repointed when an acceptance makes another item (D6) or finds a
-- duplicate (D19); no cascade, items are never deleted. proposal is the latest parsed proposal (C2) and
-- proposal_session_id the session that wrote it. rounds counts the rounds started (D11). open_on_github is false while the
-- issue is closed on GitHub, and nothing posts then (D13). last_posted is a jsonb object of progress milestone -> value
-- already posted, so each milestone posts once.
CREATE TABLE github_issue (
    id                  uuid PRIMARY KEY,
    host                text NOT NULL,
    repository          text NOT NULL,
    number              integer NOT NULL CHECK (number >= 1),
    project_id          uuid NOT NULL REFERENCES project (id),
    work_item_id        uuid NOT NULL REFERENCES work_item (id),
    author_login        text NOT NULL,
    phase               text NOT NULL CHECK (phase IN ('proposing', 'awaitingReply', 'alreadyShipped', 'waitingForMaintainer', 'accepted', 'closed')),
    proposal            jsonb,
    proposal_session_id uuid REFERENCES agent_session (id),
    rounds              integer NOT NULL DEFAULT 0 CHECK (rounds >= 0),
    open_on_github      boolean NOT NULL DEFAULT true,
    last_posted         jsonb NOT NULL DEFAULT '{}' CHECK (jsonb_typeof(last_posted) = 'object'),
    created_at          timestamptz NOT NULL,
    updated_at          timestamptz NOT NULL,
    UNIQUE (host, repository, number)
);
CREATE INDEX ix_github_issue_work_item ON github_issue (work_item_id);
CREATE INDEX ix_github_issue_accepted_open ON github_issue (project_id) WHERE phase = 'accepted' AND open_on_github;

-- The effect ledger (D18): a step that creates something claims its key with the new id inside the create's own
-- transaction, so a retried or concurrent step finds the claim instead of creating twice.
CREATE TABLE github_issue_effect (
    key             text PRIMARY KEY,
    github_issue_id uuid REFERENCES github_issue (id),
    result          uuid,
    created_at      timestamptz NOT NULL DEFAULT now()
);

-- Every GitHub write (D15), delivered one at a time per issue in created_at order. dedupe_key makes an enqueue
-- idempotent. leased_until is a sender's lease; last_result is what the last attempt returned (an ambiguous POST is
-- reconciled through the hidden outbox marker before it is sent again).
CREATE TABLE github_outbox (
    id                uuid PRIMARY KEY,
    github_issue_id   uuid NOT NULL REFERENCES github_issue (id),
    kind              text NOT NULL CHECK (kind IN ('comment', 'close', 'reopen')),
    body              text,
    reason            text,
    dedupe_key        text NOT NULL UNIQUE,
    attempts          integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at   timestamptz NOT NULL,
    leased_until      timestamptz,
    sent_at           timestamptz,
    failed_at         timestamptz,
    github_comment_id bigint,
    last_result       text CHECK (last_result IN ('ok', 'retry', 'ambiguous', 'failed')),
    last_error        text,
    created_at        timestamptz NOT NULL
);
CREATE INDEX ix_github_outbox_issue_pending ON github_outbox (github_issue_id, created_at) WHERE sent_at IS NULL AND failed_at IS NULL;
CREATE INDEX ix_github_outbox_due ON github_outbox (next_attempt_at) WHERE sent_at IS NULL AND failed_at IS NULL;

-- Durable multi-step work (D18): a round, a proposal or an acceptance, enqueued in the transaction of the state change
-- that asks for it, claimed with FOR UPDATE SKIP LOCKED and a lease, retried with backoff. key makes an enqueue idempotent.
-- attempts counts claims; created_at also feeds the per-project rate limit (D16).
CREATE TABLE issue_job (
    id              uuid PRIMARY KEY,
    kind            text NOT NULL CHECK (kind IN ('round', 'proposal', 'accept')),
    key             text NOT NULL UNIQUE,
    github_issue_id uuid NOT NULL REFERENCES github_issue (id),
    project_id      uuid NOT NULL REFERENCES project (id),
    payload         jsonb NOT NULL DEFAULT '{}',
    status          text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'done', 'failed')),
    attempts        integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at timestamptz NOT NULL,
    leased_until    timestamptz,
    last_error      text,
    created_at      timestamptz NOT NULL,
    done_at         timestamptz
);
CREATE INDEX ix_issue_job_due ON issue_job (kind, next_attempt_at) WHERE status = 'pending';
CREATE INDEX ix_issue_job_project_kind ON issue_job (project_id, kind, created_at);
