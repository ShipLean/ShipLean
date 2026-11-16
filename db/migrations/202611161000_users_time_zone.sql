-- The viewer's time zone (SHIP-1702, D1): an IANA id such as 'Asia/Bangkok', used only at the last mile to turn UTC instants
-- into labels. NULL means not set yet, and labels read in UTC. The CHECK only shapes the value; the app accepts an id only
-- when DisplayDate.TryZone knows it, and reads an id this host does not know as not set.
ALTER TABLE app_user ADD COLUMN time_zone text
    CONSTRAINT app_user_time_zone_check
    CHECK (length(time_zone) <= 64 AND time_zone ~ '^[A-Za-z][A-Za-z0-9_+-]*(/[A-Za-z0-9_+-]+)*$');

-- Every user who exists before the setting is in Bangkok (Q1). Users created later start NULL and the browser fills it in.
UPDATE app_user SET time_zone = 'Asia/Bangkok' WHERE time_zone IS NULL;
