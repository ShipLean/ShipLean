import { Link } from 'react-router-dom';
import type { RunHeaderDto, RunLinkSource } from '@shiplean/core/api/inspection';
import { routePath } from '../../routes';
import { inspectionTestIds as ids } from './testIds';
import { formatDuration } from './waterfall/geometry';
import './page/runpage.css';

export interface RunHeaderProps {
  project: string;
  itemKey: string;
  header: RunHeaderDto;
  errorsOnly: boolean;
  onErrorsOnly: (on: boolean) => void;
  query: string;
  onQuery: (text: string) => void;
}

const LINK_SOURCE: Record<RunLinkSource, string> = {
  tagged: 'Tagged',
  fromToolCall: 'From a tool call',
  shipleanFile: 'From the .shiplean file',
};

const count = (n: number | null) => (n === null ? '—' : n.toLocaleString('en-US'));

function Stat({
  id,
  label,
  value,
  tone,
}: {
  id: string;
  label: string;
  value: string;
  tone?: string;
}) {
  return (
    <span className="rv-stat">
      <b data-testid={id} className={tone}>
        {value}
      </b>
      <span>{label}</span>
    </span>
  );
}

// The run page's header: agent, start, who, link source, Older and Newer, totals, All or Errors only, find a span.
export function RunHeader({
  project,
  itemKey,
  header,
  errorsOnly,
  onErrorsOnly,
  query,
  onQuery,
}: RunHeaderProps) {
  const run = (runId: string) => routePath('agentRun', { key: project, itemKey, runId });
  const unit = header.agent === 'cursor' ? 'Prompts' : 'Turns';
  return (
    <header className="rv-head" data-testid={ids.runHeader}>
      <div className="rv-top">
        <Link
          className="rv-back"
          data-testid={ids.runBack}
          to={routePath('workItem', { key: project, itemKey })}
        >
          ← Back to {itemKey}
        </Link>
      </div>
      <div className="rv-title">
        <h1>
          <span data-testid={ids.runAgent}>{header.agentLabel}</span> run ·{' '}
          <span data-testid={ids.runStarted}>{header.startedLabel}</span>
        </h1>
        {header.startedBy === null ? null : <span className="rv-muted">{header.startedBy}</span>}
        <span className="rv-pill" data-testid={ids.runLinkSource} data-source={header.linkSource}>
          Linked: {LINK_SOURCE[header.linkSource]}
        </span>
        <span className="rv-spacer" />
        <span className="rv-nav">
          {header.olderRunId === null ? (
            <span data-testid={ids.runOlder} aria-disabled="true" className="rv-btn off">
              ‹ Older
            </span>
          ) : (
            <Link data-testid={ids.runOlder} className="rv-btn" to={run(header.olderRunId)}>
              ‹ Older
            </Link>
          )}
          {header.newerRunId === null ? (
            <span data-testid={ids.runNewer} aria-disabled="true" className="rv-btn off">
              Newer ›
            </span>
          ) : (
            <Link data-testid={ids.runNewer} className="rv-btn" to={run(header.newerRunId)}>
              Newer ›
            </Link>
          )}
        </span>
      </div>
      <div className="rv-bar">
        <Stat id={ids.runTotalTook} label="Took" value={formatDuration(header.tookMs)} />
        <Stat id={ids.runTotalTurns} label={unit} value={count(header.turns)} />
        <Stat
          id={ids.runTotalSpans}
          label={header.agent === 'claude-code' ? 'Spans' : 'Events'}
          value={count(header.spansOrEvents)}
        />
        <Stat
          id={ids.runTotalTokens}
          label="Tokens in / out"
          value={`${count(header.tokensIn)} / ${count(header.tokensOut)}`}
        />
        <Stat
          id={ids.runTotalErrors}
          label="Errors"
          value={count(header.errors)}
          tone={header.errors > 0 ? 'bad' : undefined}
        />
        {header.waitMs > 0 ? (
          <Stat
            id={ids.runTotalWait}
            label="Permission waits"
            value={formatDuration(header.waitMs)}
            tone="warn"
          />
        ) : null}
        <span className="rv-sep" />
        <span className="rv-seg" role="group" aria-label="Show">
          <button
            type="button"
            data-testid={ids.runAllRows}
            aria-pressed={!errorsOnly}
            onClick={() => onErrorsOnly(false)}
          >
            All
          </button>
          <button
            type="button"
            data-testid={ids.runErrorsOnly}
            aria-pressed={errorsOnly}
            onClick={() => onErrorsOnly(true)}
          >
            Errors only · {header.errors}
          </button>
        </span>
        <input
          className="rv-find"
          data-testid={ids.runFind}
          type="search"
          placeholder="Find a span or tool"
          aria-label="Find"
          value={query}
          onChange={(e) => onQuery(e.target.value)}
        />
      </div>
    </header>
  );
}
