-- Link provider settings an instance admin sets in Instance settings > Integrations (#2). Tokens are stored encrypted
-- (ASP.NET Data Protection, purpose of their own); the last four characters are kept beside them so the screen can show
-- them without decrypting. An unset value falls back to the SHIPLEAN_GITHUB_TOKEN, SHIPLEAN_GITLAB_URL and
-- SHIPLEAN_GITLAB_TOKEN environment variables. The GitLab token belongs to the GitLab URL stored with it.
ALTER TABLE instance_settings
    ADD COLUMN github_token          text,
    ADD COLUMN github_token_last4    text,
    ADD COLUMN gitlab_url            text,
    ADD COLUMN gitlab_token          text,
    ADD COLUMN gitlab_token_last4    text,
    ADD COLUMN preview_allow_hosts   text[] NOT NULL DEFAULT '{}';
