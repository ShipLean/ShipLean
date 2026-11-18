import { Link } from 'react-router-dom';
import type { AgentRunRowDto } from '@shiplean/core/api/inspection';
import { routePath } from '../../routes';
import { useAgentRuns } from './inspectionApi';
import { formatTokens, formatTotalDuration, sumTokens } from './section/format';
import { inspectionTestIds as ids } from './testIds';

export interface RailAgentRunsProps {
  /** The project key. */
  project: string;
  itemKey: string;
}

const facts = (row: AgentRunRowDto) => {
  const tokens = sumTokens(row.tokensIn, row.tokensOut);
  return [
    row.startedLabel,
    formatTotalDuration(row.tookMs),
    tokens === null ? null : `${formatTokens(tokens)} tokens`,
  ]
    .filter((fact) => fact !== null)
    .join(' · ');
};

// One item's agent runs for a narrow rail (the Feature timeline's): a link per run to its run page, newest first as
// the server sends them. It reads its own data, the item page's request, so mount it only when Inspection is on.
export function RailAgentRuns({ project, itemKey }: RailAgentRunsProps) {
  const runs = useAgentRuns(project, itemKey);
  const data = runs.data;
  let body;
  if (data === undefined)
    body = runs.isError ? (
      <p className="m-0 text-ink-muted" data-testid={ids.railAgentRunsNote}>
        Couldn&apos;t load agent runs.
      </p>
    ) : (
      <p className="m-0 text-ink-muted" data-testid={ids.railAgentRunsNote}>
        Loading…
      </p>
    );
  else if (!data.storeReachable)
    body = (
      <p className="m-0 text-ink-muted" data-testid={ids.railAgentRunsNote}>
        Can&apos;t reach the trace store right now.
      </p>
    );
  else if (data.rows.length === 0)
    body = (
      <p className="m-0 text-ink-muted" data-testid={ids.railAgentRunsNote}>
        None found in the last {data.store.retentionDays} days.
      </p>
    );
  else
    body = (
      <ul className="m-0 grid list-none gap-1.5 p-0">
        {data.rows.map((row) => (
          <li key={row.runId} data-testid={ids.railAgentRunsRow} data-run-id={row.runId}>
            <Link
              to={routePath('agentRun', { key: project, itemKey, runId: row.runId })}
              className="text-signal no-underline hover:underline"
            >
              {row.agentLabel}
            </Link>
            {row.errors > 0 && (
              <span className="text-danger">
                {' '}
                · {row.errors} {row.errors === 1 ? 'error' : 'errors'}
              </span>
            )}
            <div className="text-ink-muted">{facts(row)}</div>
          </li>
        ))}
      </ul>
    );
  return (
    <section data-testid={ids.railAgentRuns} className="text-[length:var(--text-sm)]">
      <h3 className="m-0 mb-1 text-[length:var(--text-sm)] font-semibold">Agent runs</h3>
      {body}
    </section>
  );
}
