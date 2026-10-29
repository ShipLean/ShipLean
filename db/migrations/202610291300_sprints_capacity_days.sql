-- Days-based capacity (SHIP-240). The sprint stores the velocity the team typed (null on the old points-only sprints); a member's
-- row stores the days available (null = the default, working days minus public holidays). capacity_points stays the computed value.
ALTER TABLE sprint ADD COLUMN capacity_velocity numeric(6, 2) NULL CHECK (capacity_velocity >= 0);
ALTER TABLE sprint_capacity ADD COLUMN days numeric(4, 1) NULL CHECK (days >= 0);
