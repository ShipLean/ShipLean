-- How a project names the sprints it makes by default (SHIP-232). The column default stays 'number': the seed projects and every existing project keep "Sprint N"; ProjectFactory writes 'yearQuarterNumber' for a new one.
ALTER TABLE project
    ADD COLUMN sprint_naming text NOT NULL DEFAULT 'number' CHECK (sprint_naming IN ('yearQuarterNumber', 'number'));
