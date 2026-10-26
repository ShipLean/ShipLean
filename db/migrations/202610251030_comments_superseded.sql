-- A comment any member marked superseded: it stays, struck through, with who and when. The first mark is kept.
ALTER TABLE comment ADD COLUMN superseded_at timestamptz;
ALTER TABLE comment ADD COLUMN superseded_by uuid REFERENCES app_user (id);
