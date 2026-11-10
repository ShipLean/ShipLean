-- The Inbox's @mentions and question options (SHIP-916 / SHIP-926 contract K1). Additive only, and nothing here is ever deleted:
-- seen, replied and withdrawn are states. A mention is a person named as `@Full Name` in a comment; the server finds them when the
-- comment is saved, one row per comment and person.
CREATE TABLE comment_mention (
    id               uuid PRIMARY KEY,
    comment_id       uuid NOT NULL REFERENCES comment (id),
    work_item_id     uuid NOT NULL REFERENCES work_item (id),
    user_id          uuid NOT NULL REFERENCES app_user (id),
    state            text NOT NULL DEFAULT 'open' CHECK (state IN ('open', 'seen', 'replied', 'withdrawn')),
    created_at       timestamptz NOT NULL,
    closed_at        timestamptz,
    reply_comment_id uuid REFERENCES comment (id),
    CONSTRAINT ux_comment_mention_comment_user UNIQUE (comment_id, user_id)
);
CREATE INDEX ix_comment_mention_open ON comment_mention (user_id) WHERE state = 'open';

-- Optional answers an asker offers on a question, shown as buttons on the phone and as quick answers on the web. At most 6.
ALTER TABLE question
    ADD COLUMN options jsonb NOT NULL DEFAULT '[]'::jsonb
        CONSTRAINT ck_question_options CHECK (jsonb_typeof(options) = 'array' AND jsonb_array_length(options) <= 6);
