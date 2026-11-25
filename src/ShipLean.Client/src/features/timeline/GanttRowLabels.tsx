import type { ReactNode } from 'react';
import type { GanttRowLabelsProps } from './GanttRowLabelsProps';
import { timelineTestIds as ids } from './testIds';
import { AXIS_HEIGHT, ROW_HEIGHT } from './chart/timelineGeometry';
import type { TimelineRow } from './chart/timelineRows';
import { PersonAvatar } from './parts/PersonAvatar';
import { TypeBadge } from './parts/TypeBadge';
import { rowTook } from './rowTook';
import type { TimelineOptions } from './TimelineOptions';

export const LEFT_WIDTH = 340;

const HEADINGS: Record<TimelineOptions['group'], string> = {
  stories: 'STORY · TASK',
  people: 'PERSON · TASK',
  tasks: 'TASK',
};

/** The chart's left column: a label per row, lined up with the SVG's rows. */
export function GanttRowLabels({ view, rows, options, selected, onSelect }: GanttRowLabelsProps) {
  const cells = (row: TimelineRow): ReactNode => {
    const took = rowTook(row, view);
    const tookCell = (
      <span
        data-testid={ids.chartRowTook}
        title={took.title}
        className={`ml-auto flex-none text-[length:var(--text-xs)] tabular-nums ${took.open ? '' : 'text-ink-muted'}${took.italic ? ' italic' : ''}`}
        style={took.open ? { color: 'var(--tl-accent)' } : undefined}
      >
        {took.text}
      </span>
    );
    if (row.kind === 'group') {
      return (
        <>
          <PersonAvatar person={row.person} size="sm" />
          <span className="truncate font-semibold">{row.label}</span>
          {tookCell}
        </>
      );
    }
    const item = row.kind === 'bracket' ? row.story : row.kind === 'bar' ? row.bar : row.item;
    const person = row.kind === 'notStarted' ? row.item.assignee : item.assignee;
    const critical = options.cp && view.finished && row.kind === 'bar' && row.bar.onCriticalPath;
    return (
      <>
        <TypeBadge type={item.type} />
        <span className="flex-none font-mono text-[length:var(--text-xs)] text-ink-muted">
          {row.key}
        </span>
        <span className="truncate" title={item.title}>
          {item.title}
        </span>
        {critical && (
          <span
            data-testid={ids.chartRowCp}
            role="img"
            aria-label="On the critical path"
            title="On the critical path"
            className="size-1.5 flex-none rounded-full"
            style={{ background: 'var(--color-text)' }}
          />
        )}
        <span className="ml-auto flex flex-none items-center gap-2">
          <PersonAvatar person={person} size="sm" />
        </span>
        {tookCell}
      </>
    );
  };

  return (
    <div
      data-testid={ids.chartLeft}
      className="sticky left-0 z-10 shrink-0 border-r border-edge bg-panel text-[length:var(--text-sm)]"
      style={{ width: LEFT_WIDTH }}
    >
      <div
        data-testid={ids.chartLeftHead}
        className="flex items-end justify-between px-3 pb-2 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted"
        style={{ height: AXIS_HEIGHT }}
      >
        <span>{HEADINGS[options.group]}</span>
        <span>TOOK</span>
      </div>
      {rows.map((row) => {
        const selectable = row.kind === 'bar' || row.kind === 'bracket';
        const isSelected = selectable && row.key === selected;
        const indent =
          options.group === 'stories' &&
          (row.kind === 'bar' || (row.kind === 'notStarted' && row.item.type === 'task'));
        const storyRow =
          row.kind === 'bracket' || (row.kind === 'notStarted' && row.item.type !== 'task');
        const className = `relative flex w-full items-center gap-2 overflow-hidden pr-3 text-left whitespace-nowrap${row.kind === 'notStarted' ? ' text-ink-muted' : ''}`;
        const style = {
          height: ROW_HEIGHT,
          paddingLeft: indent ? 28 : 12,
          fontWeight: storyRow ? 600 : 400,
          background: isSelected
            ? 'var(--tl-selected-band)'
            : storyRow || row.kind === 'group'
              ? 'var(--tl-story-band)'
              : undefined,
        };
        const common = {
          'data-testid': ids.chartRow,
          'data-key': row.kind === 'group' ? undefined : row.key,
          'data-kind': row.kind,
          'data-selected': isSelected ? true : undefined,
          className,
          style,
        };
        const accent = isSelected && (
          <span
            data-testid={ids.chartRowAccent}
            aria-hidden="true"
            className="absolute inset-y-0 left-0 w-[3px]"
            style={{ background: 'var(--tl-selected-bar)' }}
          />
        );
        return selectable ? (
          <button
            key={row.key}
            type="button"
            {...common}
            aria-pressed={isSelected}
            onClick={() => onSelect(row.key)}
          >
            {accent}
            {cells(row)}
          </button>
        ) : (
          <div key={row.key} {...common}>
            {cells(row)}
          </div>
        );
      })}
    </div>
  );
}
