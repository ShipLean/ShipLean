import { useState } from 'react';
import type { RunPageDto, RunRowDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { EventTimeline } from '../../EventTimeline';
import { RowPanel } from '../../RowPanel';
import { RunBanners } from '../../RunBanners';
import { Waterfall } from '../../Waterfall';
import { claudeRunPage, logOnlyRunPage } from '../../ct/inspectionMocks';

// The log-only run page's banner, timeline and panel with their own selection state. The log-only pages are built here
// from event rows because the pinned shapes hold no log-only run page: the rows mirror what the server produces for the
// codex-cli and cursor fixtures (a bar's end is the event, its start is the event minus duration_ms; no duration = null).
export type Variant = 'codex' | 'cursor' | 'hidden' | 'truncated';

const ev = (
  id: string,
  name: string,
  endMs: number,
  durationMs: number | null,
  isError = false,
): RunRowDto => ({
  id,
  parentId: null,
  depth: 0,
  kind: 'event',
  name,
  startMs: durationMs === null ? endMs : endMs - durationMs,
  endMs: durationMs === null ? null : endMs,
  durationMs,
  isError,
  isRetry: false,
  waitStartMs: null,
  waitEndMs: null,
  collapsedChildCount: 0,
  hasLogs: false,
});

const turn = (
  number: number,
  label: string,
  prompt: string,
  rows: RunRowDto[],
  hidden: boolean,
): RunTurnDto => ({
  number,
  label,
  prompt: hidden ? null : prompt,
  promptHidden: hidden,
  rows,
});

const codexTurns = (hidden: boolean): RunTurnDto[] => [
  turn(
    1,
    'Turn 1',
    'Fix the failing test',
    [
      ev('log-1', 'codex.user_prompt', 0, null),
      ev('log-2', 'codex.api_request', 5000, 4200),
      ev('log-3', 'codex.tool_result', 6000, 800),
      ev('log-4', 'codex.tool_result', 12000, 5000),
      ev('log-5', 'codex.tool_result', 20000, 300, true),
    ],
    hidden,
  ),
  turn(
    2,
    'Turn 2',
    'Now commit it',
    [
      ev('log-6', 'codex.user_prompt', 30000, null),
      ev('log-7', 'codex.api_request', 36000, 5000),
      ev('log-8', 'codex.tool_decision', 37000, null),
      ev('log-9', 'codex.tool_result', 40000, 900),
    ],
    hidden,
  ),
];

const cursorTurns: RunTurnDto[] = [
  turn(
    1,
    'Prompt 1',
    'Why does the vault client retry only once on 429?',
    [
      ev('cu-1', 'beforeSubmitPrompt', 0, null),
      ev('cu-2', 'beforeMCPExecution', 4200, null),
      ev('cu-3', 'beforeReadFile', 9800, null),
      ev('cu-4', 'beforeShellExecution', 31600, 600),
      ev('cu-5', 'stop', 214000, null),
    ],
    false,
  ),
  turn(
    2,
    'Prompt 2',
    'Add that to the comment above SendWithRetryAsync.',
    [ev('cu-6', 'beforeSubmitPrompt', 340000, null), ev('cu-7', 'stop', 371000, null)],
    false,
  ),
];

export function pageFor(variant: Variant): RunPageDto {
  if (variant === 'truncated') {
    return {
      ...claudeRunPage,
      truncated: true,
      grafanaUrl: 'https://grafana.example.com/explore?traceId=abc',
    };
  }
  const base = { ...logOnlyRunPage, grafanaUrl: null, truncated: false };
  if (variant === 'cursor') {
    return {
      ...base,
      banner:
        'Cursor sends hook events only: no model-call timing and no token counts. Events with no duration show as diamonds.',
      axis: { startMs: 0, endMs: 371000 },
      turns: cursorTurns,
    };
  }
  return {
    ...base,
    banner:
      'Codex CLI sends events, not spans. Bars end at each event and reach back by its duration; events with no duration show as diamonds.',
    axis: { startMs: 0, endMs: 40000 },
    turns: codexTurns(variant === 'hidden'),
  };
}

export function TimelineHarness({ variant, initial }: { variant: Variant; initial?: string }) {
  const [selected, setSelected] = useState<string | null>(initial ?? null);
  const page = pageFor(variant);
  return (
    <div style={{ width: 1440 }}>
      <RunBanners page={page} />
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: selected === null ? '1fr' : 'minmax(0,1fr) 400px',
        }}
      >
        {page.logOnly ? (
          <EventTimeline
            turns={page.turns}
            axis={page.axis}
            selectedRowId={selected}
            onSelect={setSelected}
          />
        ) : (
          <Waterfall
            turns={page.turns}
            axis={page.axis}
            selectedRowId={selected}
            onSelect={setSelected}
            contentShown={page.contentShown}
          />
        )}
        {selected === null ? null : (
          <RowPanel project="SHIP" itemKey="SHIP-1" runId="sess-cx-1" rowId={selected} />
        )}
      </div>
    </div>
  );
}
