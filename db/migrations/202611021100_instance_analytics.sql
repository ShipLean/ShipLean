-- Usage analytics, per instance: the provider (none until an admin turns it on), where it lives, the site key, and whether people's
-- names and emails go out with the events. Additive: an existing row gets provider none, so nothing is sent until it is configured.
ALTER TABLE instance_settings
    ADD COLUMN IF NOT EXISTS analytics_provider text NOT NULL DEFAULT 'none',
    ADD COLUMN IF NOT EXISTS analytics_host text NULL,
    ADD COLUMN IF NOT EXISTS analytics_site_key text NULL,
    ADD COLUMN IF NOT EXISTS analytics_send_names boolean NOT NULL DEFAULT false;

ALTER TABLE instance_settings
    DROP CONSTRAINT IF EXISTS instance_settings_analytics_provider_check;
ALTER TABLE instance_settings
    ADD CONSTRAINT instance_settings_analytics_provider_check CHECK (analytics_provider IN ('none', 'umami', 'posthog'));
