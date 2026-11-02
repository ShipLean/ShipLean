-- Which Google account signs in as this user: the ID token's sub, which never changes and is never reused, unlike an
-- email address. Null until the user first signs in with Google.
ALTER TABLE app_user ADD COLUMN google_subject text;
CREATE UNIQUE INDEX ux_app_user_google_subject ON app_user (google_subject) WHERE google_subject IS NOT NULL;
