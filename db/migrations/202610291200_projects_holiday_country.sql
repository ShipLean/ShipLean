-- The country whose public holidays a project's sprints skip (SHIP-238). Null means None.
ALTER TABLE project
    ADD COLUMN holiday_country char(2) NULL REFERENCES holiday_country (code);
