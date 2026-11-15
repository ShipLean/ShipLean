import { useEffect, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiRequestError } from '@shiplean/core/api/client';
import {
  postInspectionUsage,
  type AgentRunRowDto,
  type AgentRunsSectionDto,
} from '@shiplean/core/api/inspection';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { routePath } from '../../routes';
import { useAgentRuns } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';
import {
  formatTokens,
  formatTotalDuration,
  LINK_SOURCE_LABEL,
  LINK_SOURCE_WHY,
  SETUP_AGENTS,
  sumTokens,
} from './section/format';
import { formatDuration } from './waterfall/geometry';
import './section/section.css';

export interface AgentRunsSectionProps {
  project: ProjectDto;
  itemKey: string;
}

// The Agent runs section of the item page. It reads its own data (one request). Newest run first, as the server sends it.
export function AgentRunsSection({ project, itemKey }: AgentRunsSectionProps) {
  const runs = useAgentRuns(project.key, itemKey);
  const data = runs.data;
  const hasRuns = data !== undefined && data.storeReachable && data.rows.length > 0;

  // sectionSeen (D22): once per view of an item, and only when the section shows runs.
  const seenFor = useRef<string | null>(null);
  useEffect(() => {
    if (!hasRuns || seenFor.current === itemKey) return;
    seenFor.current = itemKey;
    postInspectionUsage(project.id, { action: 'sectionSeen', itemKey }).catch(() => undefined);
  }, [hasRuns, project.id, itemKey]);

  return (
    <section data-testid={ids.agentRunsSection} aria-label="Agent runs">
      {data === undefined ? (
        <Pending failed={runs.isError} error={runs.error} onRetry={() => void runs.refetch()} />
      ) : !data.storeReachable ? (
        <Down data={data} onRetry={() => void runs.refetch()} />
      ) : data.rows.length === 0 ? (
        <Empty data={data} itemKey={itemKey} />
      ) : (
        <WithRuns data={data} project={project.key} itemKey={itemKey} />
      )}
    </section>
  );
}

function Pending({
  failed,
  error,
  onRetry,
}: {
  failed: boolean;
  error: unknown;
  onRetry: () => void;
}) {
  if (failed) {
    return (
      <>
        <div className="ar-head">
          <h2>Agent runs</h2>
        </div>
        <p className="ar-note warn" data-testid={ids.agentRunsLoadFailed}>
          <b>Couldn&apos;t load agent runs.</b>
          {error instanceof ApiRequestError ? ` ${error.message}` : ''}
          <span>
            <button type="button" className="ar-btn" onClick={onRetry}>
              Try again
            </button>
          </span>
        </p>
      </>
    );
  }
  return (
    <>
      <div className="ar-head">
        <h2>Agent runs</h2>
      </div>
      <p className="ar-sum" data-testid={ids.agentRunsLoading}>
        Loading…
      </p>
    </>
  );
}

function Down({ data, onRetry }: { data: AgentRunsSectionDto; onRetry: () => void }) {
  return (
    <>
      <div className="ar-head">
        <h2>Agent runs</h2>
        <span className="ar-spacer" />
        <button type="button" className="ar-btn" onClick={onRetry}>
          Try again
        </button>
      </div>
      <div className="ar-note warn" data-testid={ids.agentRunsDown}>
        <span>
          <b>Can&apos;t reach the trace store right now.</b>{' '}
          {data.problem ?? 'Runs can’t be listed.'}
        </span>
        <span>The rest of this item works as usual.</span>
      </div>
    </>
  );
}

function Empty({ data, itemKey }: { data: AgentRunsSectionDto; itemKey: string }) {
  return (
    <>
      <div className="ar-head">
        <h2>Agent runs</h2>
        <span className="ar-sum">None found</span>
      </div>
      <div className="ar-note" data-testid={ids.agentRunsEmpty}>
        <span>
          No agent runs for {itemKey} in the last {data.store.retentionDays} days. A run shows here
          when the agent&apos;s telemetry is tagged with <code>shiplean.item = {itemKey}</code>, or
          when it calls ShipLean&apos;s tools about {itemKey} (<code>get_brief</code>,{' '}
          <code>pick_up</code>, <code>report_task</code> …).
        </span>
        <span>Set up the agent that works on this item:</span>
        <span className="ar-agents">
          {SETUP_AGENTS.map((a) => (
            <Link
              key={a.agent}
              data-testid={ids.agentRunsEmptySetupLink}
              data-agent={a.agent}
              to={`${data.setupUrl}?agent=${a.agent}`}
            >
              {a.label}
            </Link>
          ))}
        </span>
      </div>
    </>
  );
}

function WithRuns({
  data,
  project,
  itemKey,
}: {
  data: AgentRunsSectionDto;
  project: string;
  itemKey: string;
}) {
  const t = data.totals;
  const tokens = sumTokens(t.tokensIn, t.tokensOut);
  return (
    <>
      <div className="ar-head" data-testid={ids.agentRunsHeader}>
        <h2>Agent runs</h2>
        <span className="ar-sum">
          <span data-testid={ids.agentRunsTotalRuns}>
            {t.runs} {t.runs === 1 ? 'run' : 'runs'}
          </span>
          {' · '}
          <span data-testid={ids.agentRunsTotalTook}>{formatTotalDuration(t.tookMs)}</span>
          {' · '}
          <span data-testid={ids.agentRunsTotalTokens}>{formatTokens(tokens)} tokens</span>
        </span>
        <span className="ar-spacer" />
        {t.errors > 0 && (
          <span className="ar-pill danger" data-testid={ids.agentRunsTotalErrors}>
            {t.errors} {t.errors === 1 ? 'error' : 'errors'}
          </span>
        )}
        {t.waitMs > 0 && (
          <span
            className="ar-pill warn"
            data-testid={ids.agentRunsTotalWait}
            title="Time the agent sat waiting for someone to allow a tool"
          >
            Waited {formatTotalDuration(t.waitMs)} for permission
          </span>
        )}
      </div>
      <div className="ar-box">
        <table className="ar-table">
          <thead>
            <tr>
              <th>Agent</th>
              <th>What it did</th>
              <th className="r">Took</th>
              <th>Timeline</th>
              <th className="r">Tokens</th>
              <th className="r">Errors</th>
            </tr>
          </thead>
          <tbody>
            {data.rows.map((row) => (
              <RunRow key={row.runId} row={row} project={project} itemKey={itemKey} />
            ))}
          </tbody>
        </table>
      </div>
      <div className="ar-foot" data-testid={ids.agentRunsFooter}>
        <span data-testid={ids.agentRunsStore}>
          {data.store.mode === 'clickhouse' ? 'ClickHouse' : 'Tempo and Loki'} · kept{' '}
          {data.store.retentionDays} days
        </span>
        <span data-testid={ids.agentRunsContentNote}>
          {data.contentShown
            ? '· prompts and tool output shown'
            : '· prompts and tool output hidden'}
        </span>
        <span className="ar-spacer" />
        <Link data-testid={ids.agentRunsSetupLink} to={data.setupUrl}>
          Agent setup
        </Link>
      </div>
    </>
  );
}

function RunRow({
  row,
  project,
  itemKey,
}: {
  row: AgentRunRowDto;
  project: string;
  itemKey: string;
}) {
  const navigate = useNavigate();
  const to = routePath('agentRun', { key: project, itemKey, runId: row.runId });
  const unit = row.logOnly ? 'events, no trace' : 'spans';
  const tokens = sumTokens(row.tokensIn, row.tokensOut);
  return (
    <tr
      className="ar-row"
      data-testid={ids.agentRunsRow}
      data-run-id={row.runId}
      onClick={() => void navigate(to)}
    >
      <td>
        <span className="ar-agent">
          <Link to={to} data-testid={ids.agentRunsRowAgent} onClick={(e) => e.stopPropagation()}>
            {row.agentLabel}
          </Link>
          <small data-testid={ids.agentRunsRowStarted}>
            {row.startedBy === null ? '' : `${row.startedBy} · `}
            {row.startedLabel}
          </small>
        </span>
      </td>
      <td className="what">
        <span className="ar-o" data-testid={ids.agentRunsRowWhatItDid}>
          {row.whatItDid ?? <span className="ar-muted">&mdash;</span>}
        </span>
        <span className="ar-s">
          <span
            className={`ar-pill ${row.linkSource === 'tagged' ? 'ok' : 'info'}`}
            title={`Linked by ${LINK_SOURCE_WHY[row.linkSource]}`}
            data-testid={ids.agentRunsRowLinkSource}
          >
            {LINK_SOURCE_LABEL[row.linkSource]}
          </span>{' '}
          <span data-testid={ids.agentRunsRowMeta}>
            {row.turns} {row.turns === 1 ? 'turn' : 'turns'} · {row.spanOrEventCount} {unit}
            {row.model === null ? '' : ` · ${row.model}`}
          </span>
        </span>
      </td>
      <td className="r" data-testid={ids.agentRunsRowTook}>
        {formatDuration(row.tookMs)}
      </td>
      <td>
        <span className="ar-mini" aria-hidden="true" data-testid={ids.agentRunsRowTimeline}>
          {row.timeline.map((m, i) => (
            <i
              key={i}
              className={m.kind}
              style={{ left: `${m.startPct}%`, width: `${Math.max(m.endPct - m.startPct, 0)}%` }}
            />
          ))}
        </span>
      </td>
      <td
        className="r"
        data-testid={ids.agentRunsRowTokens}
        title={
          tokens === null
            ? undefined
            : `${formatTokens(row.tokensIn)} in · ${formatTokens(row.tokensOut)} out`
        }
      >
        {tokens === null ? <span className="ar-muted">&mdash;</span> : formatTokens(tokens)}
      </td>
      <td className="r" data-testid={ids.agentRunsRowErrors}>
        {row.errors > 0 ? <span className="ar-errs">{row.errors}</span> : 0}
      </td>
    </tr>
  );
}
