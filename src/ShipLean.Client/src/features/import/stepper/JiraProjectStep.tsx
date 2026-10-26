import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import { useJiraProjects } from '../importApi';
import { importTestIds as ids } from '../testIds';
import { JiraProjectRow } from './JiraProjectRow';

// Step 2: pick the one Jira project to import. One already imported shows where it went.
export function JiraProjectStep({
  projectId,
  selected,
  onPick,
}: {
  projectId: string;
  selected: string | null;
  onPick: (key: string) => void;
}) {
  const [query, setQuery] = useState('');
  const projects = useJiraProjects(projectId, true);
  if (projects.isPending) return <p className="text-ink-muted">Reading the Jira projects…</p>;
  if (projects.isError) {
    return (
      <p data-testid={ids.projectsError} role="alert" className="text-alert">
        {projects.error instanceof ApiRequestError
          ? projects.error.message
          : 'The Jira projects could not be read.'}
      </p>
    );
  }
  const needle = query.trim().toLowerCase();
  const rows = projects.data.filter(
    (p) =>
      needle === '' ||
      p.key.toLowerCase().includes(needle) ||
      p.name.toLowerCase().includes(needle),
  );
  return (
    <section
      data-testid={ids.projectsStep}
      className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <div className="flex items-center gap-[var(--space-3)]">
        <h2 className="font-semibold">Pick one Jira project</h2>
        <input
          data-testid={ids.projectSearch}
          type="search"
          aria-label="Search Jira projects"
          className="field ml-auto w-64"
          placeholder="Search by key or name"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
      </div>
      {rows.length === 0 ? (
        <p data-testid={ids.projectsEmpty} className="text-ink-muted">
          No Jira project matches.
        </p>
      ) : (
        <table className="w-full text-[length:var(--text-sm)]">
          <thead>
            <tr className="text-left text-ink-muted">
              <th className="px-3 py-1 font-medium">Key</th>
              <th className="px-3 py-1 font-medium">Name</th>
              <th className="px-3 py-1 text-right font-medium">Issues</th>
              <th className="px-3 py-1 font-medium">In ShipLean</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((p) => (
              <JiraProjectRow
                key={p.key}
                project={p}
                selected={p.key === selected}
                onPick={() => onPick(p.key)}
              />
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
