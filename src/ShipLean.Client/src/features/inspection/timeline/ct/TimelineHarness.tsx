import { useState } from 'react';
import type { RunPageDto, RunRowDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { EventTimeline } from '../../EventTimeline';
import { RowPanel } from '../../RowPanel';
import { RunBanners } from '../../RunBanners';
import { Waterfall } from '../../Waterfall';
import cliAgentJson from './codex-cli-run-page.json';
import cursorJson from './cursor-run-page.json';
import { claudeRunPage, logOnlyRunPage } from '../../ct/inspectionMocks';

// The log-only run page's banner, timeline and panel with their own selection state. The Codex CLI and cursor runs are the
// server's own mapping of codex-cli-synthetic and cursor-synthetic (codex-cli-run-page.json, cursor-run-page.json; same
// mapping as InspectionViews.RunAsync: start = event - duration_ms, no duration = null, labels "Turn N").
const cliAgentRun = cliAgentJson as unknown as FixtureRun;
const cursorRun = cursorJson as unknown as FixtureRun;
export type Variant = 'codex-cli' | 'cursor' | 'hidden' | 'truncated';

interface FixtureRun {
  axis: { startMs: number; endMs: number };
  turns: { number: number; promptText: string | null; rows: RunRowDto[] }[];
}

// Rows and turns as InspectionViews.RunAsync renders them for the fixtures (JSON generated from the assembled fixtures).
const toTurns = (run: FixtureRun, hidden: boolean): RunTurnDto[] =>
  run.turns.map((t) => ({
    number: t.number,
    label: `Turn ${t.number}`,
    prompt: hidden ? null : t.promptText,
    promptHidden: hidden && t.promptText !== null,
    rows: t.rows,
  }));

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
      axis: cursorRun.axis,
      turns: toTurns(cursorRun, false),
    };
  }
  return {
    ...base,
    banner:
      'Codex CLI sends events, not spans. Bars end at each event and reach back by its duration; events with no duration show as diamonds.',
    axis: cliAgentRun.axis,
    turns: toTurns(cliAgentRun, variant === 'hidden'),
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
