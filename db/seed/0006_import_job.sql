-- MOB's Jira connection (base URL and email; the API token is encrypted later, so it is null) and the finished import
-- job Imported -> MOB. progress is the pinned mobFinished document; the job ran two days before the run date.
-- Ids: connection 0000000a-0000-4000-8003-000000000001, job 00000006-0000-4000-8000-000000000001.

INSERT INTO jira_connection (id, project_id, base_url, email, created_by, created_at)
SELECT '0000000a-0000-4000-8003-000000000001', project.id, 'https://larkspur.atlassian.net', 'joel@shiplean.local',
       '0199f000-0000-7000-8000-000000000001', ((now() AT TIME ZONE 'utc')::date - 10)::timestamp AT TIME ZONE 'utc'
FROM project WHERE project.key = 'MOB';

INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, finished_at, created_by)
SELECT '00000006-0000-4000-8000-000000000001', '0000000a-0000-4000-8003-000000000001', 'MOBILE', 'MOB', project.id, 'done',
       '{
  "percent": 100,
  "step": "finishing",
  "message": "Import finished",
  "rerun": false,
  "counts": {
    "sprints": {
      "done": 9,
      "total": 9,
      "failed": 0
    },
    "issues": {
      "done": 1284,
      "total": 1284,
      "failed": 0
    },
    "comments": {
      "done": 2406,
      "total": 2406,
      "failed": 0
    },
    "attachments": {
      "done": 388,
      "total": 388,
      "failed": 0
    },
    "revisions": {
      "done": 12950,
      "total": 12950,
      "failed": 0
    }
  },
  "usersMatched": 6,
  "usersCreated": 0,
  "statesCreated": 0,
  "rerunResult": null
}'::jsonb,
       (((now() AT TIME ZONE 'utc')::date - 2)::timestamp + interval '9 hours') AT TIME ZONE 'utc',
       (((now() AT TIME ZONE 'utc')::date - 2)::timestamp + interval '9 hours 14 minutes') AT TIME ZONE 'utc',
       '0199f000-0000-7000-8000-000000000001'
FROM project WHERE project.key = 'MOB';
