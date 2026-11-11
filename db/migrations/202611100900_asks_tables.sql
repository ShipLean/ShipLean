-- Decisions and questions on work items (SHIP-922, contract K1). Additive only. Nothing here is ever deleted and there is no
-- archived_at: withdrawal is a state. Numbers (D1, D2…, Q1, Q2…) are per item, counted separately for decisions and questions,
-- handed out under AskNumbers' per-item advisory lock and never reused; the unique constraints are the backstop.
CREATE TABLE decision (
    id                  uuid PRIMARY KEY,
    work_item_id        uuid NOT NULL REFERENCES work_item (id),
    number              integer NOT NULL CHECK (number > 0),
    question            text NOT NULL,
    recommended         text NOT NULL,
    alternatives        jsonb NOT NULL DEFAULT '[]'::jsonb CHECK (jsonb_typeof(alternatives) = 'array'),
    current_answer      text NOT NULL,
    answer_kind         text NOT NULL DEFAULT 'recommended' CHECK (answer_kind IN ('recommended', 'alternative', 'own')),
    alternative_index   integer CHECK (alternative_index >= 0),
    state               text NOT NULL DEFAULT 'open' CHECK (state IN ('open', 'confirmed', 'changed', 'withdrawn')),
    created_by_user_id  uuid NOT NULL REFERENCES app_user (id),
    created_agent_name  text,
    created_at          timestamptz NOT NULL DEFAULT now(),
    answered_by_user_id uuid REFERENCES app_user (id),
    answered_agent_name text,
    answered_at         timestamptz,
    from_description    boolean NOT NULL DEFAULT false,
    updated_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_decision_number UNIQUE (work_item_id, number),
    -- While open, nobody has answered: the recommended answer stands.
    CONSTRAINT ck_decision_open_answer CHECK (state <> 'open' OR (current_answer = recommended AND answer_kind = 'recommended')),
    CONSTRAINT ck_decision_alternative_index CHECK ((answer_kind = 'alternative') = (alternative_index IS NOT NULL))
);
CREATE INDEX ix_decision_open ON decision (work_item_id) WHERE state = 'open';

CREATE TABLE question (
    id                   uuid PRIMARY KEY,
    work_item_id         uuid NOT NULL REFERENCES work_item (id),
    number               integer NOT NULL CHECK (number > 0),
    text                 text NOT NULL,
    state                text NOT NULL DEFAULT 'open' CHECK (state IN ('open', 'answered', 'withdrawn')),
    created_by_user_id   uuid NOT NULL REFERENCES app_user (id),
    created_agent_name   text,
    created_at           timestamptz NOT NULL DEFAULT now(),
    answer_comment_id    uuid REFERENCES comment (id),
    answered_by_user_id  uuid REFERENCES app_user (id),
    answered_at          timestamptz,
    withdrawn_by_user_id uuid REFERENCES app_user (id),
    withdrawn_at         timestamptz,
    CONSTRAINT ux_question_number UNIQUE (work_item_id, number),
    -- The answer is a comment (D7): an answered question always links one.
    CONSTRAINT ck_question_answered CHECK (state <> 'answered' OR answer_comment_id IS NOT NULL)
);
CREATE INDEX ix_question_open ON question (work_item_id) WHERE state = 'open';

-- Append-only: every raise, edit, answer and withdrawal, for the history and for time-to-answer.
CREATE TABLE ask_event (
    id               uuid PRIMARY KEY,
    work_item_id     uuid NOT NULL REFERENCES work_item (id),
    decision_id      uuid REFERENCES decision (id),
    question_id      uuid REFERENCES question (id),
    kind             text NOT NULL CHECK (kind IN ('raised', 'edited', 'confirmed', 'changed', 'withdrawn', 'asked', 'answered')),
    from_answer      text,
    to_answer        text,
    to_kind          text CHECK (to_kind IN ('recommended', 'alternative', 'own')),
    note             text,
    by_user_id       uuid REFERENCES app_user (id),
    agent_name       text,
    at               timestamptz NOT NULL DEFAULT now(),
    from_description boolean NOT NULL DEFAULT false,
    CONSTRAINT ck_ask_event_target CHECK (num_nonnulls(decision_id, question_id) = 1)
);
CREATE INDEX ix_ask_event_item_at ON ask_event (work_item_id, at);

-- Marks the comments the section posts: answer -> "Answer to Q1", changed -> "D3 changed", about -> "On D3".
CREATE TABLE comment_ask_tag (
    comment_id  uuid PRIMARY KEY REFERENCES comment (id),
    kind        text NOT NULL CHECK (kind IN ('answer', 'changed', 'about')),
    decision_id uuid REFERENCES decision (id),
    question_id uuid REFERENCES question (id),
    created_at  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_comment_ask_tag_target CHECK (num_nonnulls(decision_id, question_id) = 1),
    -- An answer is to a question; changed and about are on a decision.
    CONSTRAINT ck_comment_ask_tag_kind CHECK ((kind = 'answer') = (question_id IS NOT NULL))
);
