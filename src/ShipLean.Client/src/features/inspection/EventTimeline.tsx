import { memo, useCallback, useEffect, useRef } from 'react';
import type { RunAxisDto, RunRowDto, RunTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';
import { formatDuration, pointPct, rowSpan, ticks, tickLabel } from './waterfall/geometry';
import './waterfall/waterfall.css';
import './timeline/timeline.css';

export interface EventTimelineProps {
  turns: RunTurnDto[];
  axis: RunAxisDto;
  selectedRowId: string | null;
  onSelect: (rowId: string | null) => void;
}

interface RowProps {
  row: RunRowDto;
  axis: RunAxisDto;
  selected: boolean;
  onSelect: (rowId: string | null) => void;
}

// One flat event row (no nesting). A row with a duration draws its bar back from the event; one without is a diamond.
const EventRow = memo(function EventRow({ row, axis, selected, onSelect }: RowProps) {
  const span = rowSpan(axis, row);
  const hasDuration = row.durationMs !== null && row.durationMs > 0 && row.endMs !== null;
  const point = hasDuration ? null : pointPct(axis, row.startMs);
  const tone = row.isError ? 'err' : 'event';
  const select = () => onSelect(selected ? null : row.id);
  return (
    <div
      role="row"
      tabIndex={0}
      aria-selected={selected}
      data-testid={ids.runRowEvent}
      data-row-id={row.id}
      className="rw-row"
      onClick={select}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          select();
        }
      }}
    >
      <div className="rw-lab">
        <span className="rw-kd event">•</span>
        <span className="rw-nm">{row.name}</span>
        {row.isError ? (
          <span className="rw-x" data-testid={ids.runRowError} title="Error">
            ✕
          </span>
        ) : null}
      </div>
      <div className="rw-track">
        {span === null ? null : (
          <span
            className={`rw-bar ${tone}`}
            data-testid={ids.runRowBar}
            style={{ left: `${span.left}%`, width: `${span.width}%` }}
          />
        )}
        {point === null ? null : (
          <span
            className={`rw-pt ${row.isError ? 'err' : 'event'}`}
            data-testid={ids.runRowDiamond}
            style={{ left: `${point}%` }}
          />
        )}
      </div>
      <div className="rw-dur">{row.durationMs === null ? '' : formatDuration(row.durationMs)}</div>
    </div>
  );
});

// The log-only run's flat event rows by turn on one time axis (D19).
export function EventTimeline({
  turns,
  axis,
  selectedRowId,
  onSelect: onSelectProp,
}: EventTimelineProps) {
  const latest = useRef(onSelectProp);
  useEffect(() => {
    latest.current = onSelectProp;
  });
  const onSelect = useCallback((rowId: string | null) => latest.current(rowId), []);
  return (
    <div
      role="grid"
      className="rw"
      data-testid={ids.runEvents}
      data-turns={turns.length}
      data-selected={selectedRowId ?? ''}
    >
      <div className="rw-row rw-head">
        <div className="rw-lab">Event</div>
        <div className="rw-axis" data-testid={ids.runAxis}>
          {ticks(axis).map((t) => (
            <span key={t} style={{ left: `${pointPct(axis, t) ?? 0}%` }}>
              {tickLabel(t)}
            </span>
          ))}
        </div>
        <div className="rw-dur">Took</div>
      </div>
      {turns.map((turn) => (
        <div key={turn.number} role="rowgroup" data-testid={ids.runTurn} data-turn={turn.number}>
          <div className="rw-row rw-turn">
            <div className="rw-lab">
              <span className="rw-kd turn">›</span>
              <span className="rw-nm">{turn.label}</span>
              {turn.promptHidden ? (
                <span className="rw-pr" data-testid={ids.runTurnPromptHidden}>
                  prompt hidden
                </span>
              ) : turn.prompt !== null ? (
                <span className="rw-pr" data-testid={ids.runTurnPrompt}>
                  “{turn.prompt}”
                </span>
              ) : null}
            </div>
            <div className="rw-track" />
            <div className="rw-dur" />
          </div>
          {turn.rows.map((row) => (
            <EventRow
              key={row.id}
              row={row}
              axis={axis}
              selected={row.id === selectedRowId}
              onSelect={onSelect}
            />
          ))}
        </div>
      ))}
      <div className="rw-legend">
        <span>
          <i style={{ background: 'var(--color-muted)' }} />
          Event, drawn back from its end by its duration
        </span>
        <span>
          <i className="rw-lg-pt" style={{ background: 'var(--color-muted)' }} />
          Event with no duration
        </span>
        <span>
          <i style={{ background: 'var(--color-danger)' }} />
          Error
        </span>
      </div>
    </div>
  );
}
