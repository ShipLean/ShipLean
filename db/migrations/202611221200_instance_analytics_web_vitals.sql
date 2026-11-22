-- Web vitals switch (SHIP-256): off by default; kept when the provider is none, so turning analytics back on restores it.
ALTER TABLE instance_settings
    ADD COLUMN IF NOT EXISTS analytics_web_vitals boolean NOT NULL DEFAULT false;
