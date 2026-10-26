-- SHIP's Sprints 19-26 and the capacity members entered. All dates are computed from the run date:
--   today  = the UTC date
--   anchor = the latest working day (Mon-Fri) on or before today
--   Sprint 24 starts 3 working days before anchor and ends start + 13 days (two calendar weeks)
--   Sprint N starts Sprint 24's start + (N - 24) * 14 days and ends start + 13 days.
-- Sprint status (past / current / future) is not stored; the server derives it from the dates.
-- Sprints 19-23 have rollover_status = decided, 24-26 null. Joel has no capacity rows.

INSERT INTO sprint (id, project_id, name, start_date, end_date, goal, rollover_status)
SELECT format('00000004-0000-4000-8001-%s', lpad(v.number::text, 12, '0'))::uuid,
       project.id,
       'Sprint ' || v.number,
       s24.start_date + (v.number - 24) * 14,
       s24.start_date + (v.number - 24) * 14 + 13,
       v.goal,
       v.rollover_status
FROM (VALUES
  (19, '', 'decided'),
  (20, '', 'decided'),
  (21, 'Reporting MVP', 'decided'),
  (22, 'Checkout performance', 'decided'),
  (23, 'Coupons v2', 'decided'),
  (24, 'Customers can check out with a saved card; passkey sign-in behind a flag.', NULL),
  (25, 'Remember-me and the reporting export', NULL),
  (26, '', NULL)
) AS v (number, goal, rollover_status)
CROSS JOIN (
    SELECT a.anchor - CASE WHEN extract(isodow FROM a.anchor) <= 3 THEN 5 ELSE 3 END AS start_date
    FROM (
        SELECT t.today - CASE extract(isodow FROM t.today)::int WHEN 6 THEN 1 WHEN 7 THEN 2 ELSE 0 END AS anchor
        FROM (SELECT (now() AT TIME ZONE 'utc')::date AS today) AS t
    ) AS a
) AS s24
JOIN project ON project.key = 'SHIP';

-- Capacity ids: 00000008-0000-4000-8001-<sprint number><two-digit user number>.
INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points, note)
SELECT format('00000008-0000-4000-8001-%s', lpad((v.sprint * 100 + v.person)::text, 12, '0'))::uuid,
       format('00000004-0000-4000-8001-%s', lpad(v.sprint::text, 12, '0'))::uuid,
       format('00000001-0000-4000-8000-%s', lpad(v.person::text, 12, '0'))::uuid,
       v.points, v.note
FROM (VALUES
  (21, 2, 4, 'Mei and Priya at a conference'),
  (21, 3, 4, NULL),
  (21, 4, 0, 'Mei and Priya at a conference'),
  (21, 5, 4, NULL),
  (21, 6, 4, NULL),
  (22, 2, 6, NULL),
  (22, 3, 5, NULL),
  (22, 4, 5, NULL),
  (22, 5, 5, NULL),
  (22, 6, 3, NULL),
  (23, 2, 6, NULL),
  (23, 3, 5, NULL),
  (23, 4, 5, NULL),
  (23, 5, 4, NULL),
  (23, 6, 3, NULL),
  (24, 2, 6, NULL),
  (24, 3, 5, NULL),
  (24, 4, 5, NULL),
  (24, 5, 4, NULL),
  (24, 6, 3, NULL),
  (25, 2, 6, NULL),
  (25, 3, 5, NULL),
  (25, 4, 5, NULL),
  (25, 5, 4, NULL),
  (25, 6, 2, NULL)
) AS v (sprint, person, points, note);
