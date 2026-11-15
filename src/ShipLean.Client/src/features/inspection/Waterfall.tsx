import { memo, useCallback, useEffect, useRef } from 'react';
import type { RunAxisDto, RunRowDto, RunRowKind, RunTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';
import {
  afterWait,
  formatDuration,
  pointPct,
  rowSpan,
  ticks,
  tickLabel,
  waitSpan,
} from './waterfall/geometry';
import type { Span } from './waterfall/geometry';
import './waterfall/waterfall.css';

export interface WaterfallProps {
  turns: RunTurnDto[];
  axis: RunAxisDto;
  selectedRowId: string | null;
  onSelect: (rowId: string | null) => void;
  contentShown: boolean;
}

const KIND_LETTER: Record<RunRowKind, string> = {
  modelCall: 'M',
  tool: 'T',
  shipleanTool: 'SL',
  subagent: 'S',
  event: '•',
};

const place = (s: Span) => ({ left: `${s.left}%`, width: `${s.width}%` });

interface RowProps {
  row: RunRowDto;
  axis: RunAxisDto;
  selected: boolean;
  onSelect: (rowId: string | null) => void;
}

// One memoised row: a selection change re-renders two rows, not the whole run.
const WaterfallRow = memo(function WaterfallRow({ row, axis, selected, onSelect }: RowProps) {
  const span = rowSpan(axis, row);
  const wait = waitSpan(axis, row);
  const isPoint = row.endMs === null || !row.durationMs;
  const point = isPoint ? pointPct(axis, row.startMs) : null;
  const tone = row.isRetry ? 'retry' : row.isError ? 'err' : row.kind;
  const body = wait === null ? span : afterWait(axis, row);
  const select = () => onSelect(selected ? null : row.id);
  return (
    <div
      role="row"
      tabIndex={0}
      aria-selected={selected}
      data-testid={ids.runRow}
      data-row-id={row.id}
      data-kind={row.kind}
      className="rw-row"
      onClick={select}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          select();
        }
      }}
    >
      <div className="rw-lab" style={{ ['--d' as string]: row.depth }}>
        <span className={`rw-kd ${row.kind}`}>{KIND_LETTER[row.kind]}</span>
        {row.kind === 'modelCall' ? (
          <>
            <span className="rw-nm">Model call</span>
            <span className="rw-dt">{row.name}</span>
          </>
        ) : (
          <span className="rw-nm">{row.name}</span>
        )}
        {row.collapsedChildCount > 0 ? (
          <span className="rw-pill" data-testid={ids.runRowCollapsed}>
            {row.collapsedChildCount} spans
          </span>
        ) : null}
        {wait !== null && row.waitStartMs !== null && row.waitEndMs !== null ? (
          <span className="rw-pill warn">
            waited {formatDuration(row.waitEndMs - row.waitStartMs)}
          </span>
        ) : null}
        {row.isRetry ? <span className="rw-pill">retried</span> : null}
        {row.isError ? (
          <span className="rw-x" data-testid={ids.runRowError} title="Error">
            ✕
          </span>
        ) : null}
      </div>
      <div className="rw-track">
        {wait === null ? null : (
          <span className="rw-bar wait" data-testid={ids.runRowWait} style={place(wait)} />
        )}
        {body === null ? null : (
          <span
            className={`rw-bar ${tone}`}
            data-testid={row.isRetry ? ids.runRowRetry : ids.runRowBar}
            style={place(body)}
          />
        )}
        {point === null ? null : (
          <span
            className={`rw-pt ${row.isError ? 'err' : row.kind}`}
            data-testid={ids.runRowDiamond}
            style={{ left: `${point}%` }}
          />
        )}
      </div>
      <div className="rw-dur">{row.durationMs === null ? '' : formatDuration(row.durationMs)}</div>
    </div>
  );
});

// The traced run's rows by turn on one time axis (D5, D18). Zoom is the axis the page passes in: bars clip to it.
export function Waterfall({
  turns,
  axis,
  selectedRowId,
  onSelect: onSelectProp,
  contentShown,
}: WaterfallProps) {
  // A stable handler, so a page that passes a new function each render doesn't re-render every row.
  const latest = useRef(onSelectProp);
  useEffect(() => {
    latest.current = onSelectProp;
  });
  const onSelect = useCallback((rowId: string | null) => latest.current(rowId), []);
  return (
    <div
      role="grid"
      className="rw"
      data-testid={ids.runWaterfall}
      data-turns={turns.length}
      data-selected={selectedRowId ?? ''}
    >
      <div className="rw-row rw-head">
        <div className="rw-lab">Span</div>
        <div className="rw-axis" data-testid={ids.runAxis}>
          {ticks(axis).map((t) => (
            <span key={t} style={{ left: `${pointPct(axis, t) ?? 0}%` }}>
              {tickLabel(t)}
            </span>
          ))}
        </div>
        <div className="rw-dur">Took</div>
      </div>
      {turns.map((turn) => {
        const shown = contentShown && !turn.promptHidden && turn.prompt !== null;
        return (
          <div key={turn.number} role="rowgroup" data-testid={ids.runTurn} data-turn={turn.number}>
            <div className="rw-row rw-turn">
              <div className="rw-lab">
                <span className="rw-kd turn">›</span>
                <span className="rw-nm">{turn.label}</span>
                {shown ? (
                  <span className="rw-pr" data-testid={ids.runTurnPrompt}>
                    “{turn.prompt}”
                  </span>
                ) : (
                  <span className="rw-pr" data-testid={ids.runTurnPromptHidden}>
                    prompt hidden
                  </span>
                )}
              </div>
              <div className="rw-track" />
              <div className="rw-dur" />
            </div>
            {turn.rows.map((row) => (
              <WaterfallRow
                key={row.id}
                row={row}
                axis={axis}
                selected={row.id === selectedRowId}
                onSelect={onSelect}
              />
            ))}
          </div>
        );
      })}
      <div className="rw-legend">
        <span>
          <i style={{ background: 'var(--color-purple)' }} />
          Model call
        </span>
        <span>
          <i style={{ background: 'var(--color-info)' }} />
          Tool
        </span>
        <span>
          <i style={{ background: 'var(--color-accent)' }} />
          ShipLean tool
        </span>
        <span>
          <i style={{ background: 'var(--color-ok)' }} />
          Subagent
        </span>
        <span>
          <i
            style={{
              background:
                'repeating-linear-gradient(135deg, var(--color-warn) 0 4px, transparent 4px 7px)',
            }}
          />
          Permission wait
        </span>
        <span>
          <i style={{ background: 'var(--color-danger)' }} />
          Error
        </span>
      </div>
    </div>
  );
}
