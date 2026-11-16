import { useState } from 'react';
import type { RunPageDto, RunRowDto } from '@shiplean/core/api/inspection';
import { RowPanel } from '../../RowPanel';
import { Waterfall } from '../../Waterfall';
import { claudeRunPage } from '../../ct/inspectionMocks';

// The Run page's middle (waterfall + panel) with its own selection state. The variant is a string so the spec passes
// plain data; the rows are derived here from the pinned run page.
export type Variant = 'default' | 'permission' | 'edit' | 'hidden' | 'big';

const row = (over: Partial<RunRowDto> & Pick<RunRowDto, 'id'>): RunRowDto => ({
  parentId: null,
  depth: 0,
  kind: 'tool',
  name: 'Bash',
  startMs: 0,
  endMs: 0,
  durationMs: 0,
  isError: false,
  isRetry: false,
  waitStartMs: null,
  waitEndMs: null,
  collapsedChildCount: 0,
  hasLogs: false,
  ...over,
});

export function pageFor(variant: Variant): RunPageDto {
  const t1 = claudeRunPage.turns[0];
  const extra: RunRowDto[] = [
    row({
      id: 'err-1',
      name: 'Bash',
      startMs: 21000,
      endMs: 30000,
      durationMs: 9000,
      isError: true,
    }),
    row({
      id: 'retry-1',
      kind: 'modelCall',
      name: 'claude_code.llm_request',
      startMs: 31000,
      endMs: 33000,
      durationMs: 2000,
      isRetry: true,
    }),
  ];
  let turns = [{ ...t1, rows: [...t1.rows, ...extra] }, ...claudeRunPage.turns.slice(1)];
  let axis = claudeRunPage.axis;
  if (variant === 'permission') {
    axis = { startMs: 0, endMs: 1_500_000 };
    turns = [
      {
        ...t1,
        rows: [
          row({
            id: 'perm-1',
            name: 'Bash',
            startMs: 12000,
            endMs: 1_290_000,
            durationMs: 1_278_000,
            waitStartMs: 12000,
            waitEndMs: 1_272_000,
          }),
        ],
      },
    ];
  }
  if (variant === 'big') {
    axis = { startMs: 0, endMs: 400_000 };
    turns = [
      {
        ...t1,
        rows: Array.from({ length: 400 }, (_, i) =>
          row({
            id: `r${i}`,
            startMs: i * 1000,
            endMs: i * 1000 + 800,
            durationMs: 800,
            depth: i % 3,
          }),
        ),
      },
    ];
  }
  if (variant === 'hidden') turns = turns.map((t) => ({ ...t, prompt: null, promptHidden: true }));
  return { ...claudeRunPage, axis, turns, contentShown: variant !== 'hidden' };
}

export function WaterfallHarness({ variant, initial }: { variant: Variant; initial?: string }) {
  const [selected, setSelected] = useState<string | null>(initial ?? null);
  const page = pageFor(variant);
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1fr) 400px', width: 1440 }}>
      <Waterfall
        turns={page.turns}
        axis={page.axis}
        selectedRowId={selected}
        onSelect={setSelected}
        contentShown={page.contentShown}
      />
      {selected === null ? null : (
        <RowPanel project="SHIP" itemKey="SHIP-1" runId="sess-cc-1" rowId={selected} />
      )}
    </div>
  );
}
