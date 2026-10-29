-- Public holidays by country, fetched from Nager.Date by a background job. The country list comes from the feed;
-- "tracked" marks the countries whose holidays are kept (one a project has picked). A holiday that leaves the feed is
-- archived, never deleted, and revived when it returns.
CREATE TABLE holiday_country (
    code         char(2)     PRIMARY KEY,
    name         text        NOT NULL,
    tracked      boolean     NOT NULL DEFAULT false,
    refreshed_at timestamptz
);

CREATE TABLE public_holiday (
    country_code char(2)     NOT NULL REFERENCES holiday_country (code),
    date         date        NOT NULL,
    name         text        NOT NULL,
    archived_at  timestamptz,
    PRIMARY KEY (country_code, date, name)
);
