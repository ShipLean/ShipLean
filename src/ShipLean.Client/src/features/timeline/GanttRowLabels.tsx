import type { TimelinePersonDto } from '@shiplean/core/api/views';
import type { GanttRowLabelsProps } from './GanttRowLabelsProps';
import { timelineTestIds as ids } from './testIds';
import { AXIS_HEIGHT, ROW_HEIGHT } from './chart/timelineGeometry';
import type { TimelineRow } from './chart/timelineRows';

const LEFT_WIDTH = 240;

const initials = (person: TimelinePersonDto | null): string =>
  person === null
    ? ''
    : person.name
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((word) => word[0].toUpperCase())
        .join('');

function rowLabel(row: TimelineRow): { title: string; key: string | null; who: string } {
  switch (row.kind) {
    case 'group':
      return { title: row.label, key: null, who: row.agent ? 'agent' : '' };
    case 'bracket':
      return { title: row.story.title, key: row.key, who: initials(row.story.assignee) };
    case 'bar':
      return { title: row.bar.title, key: row.key, who: initials(row.bar.assignee) };
  }
}

/** The chart's left column: a label per row, lined up with the SVG's rows. */
export function GanttRowLabels({ rows, selected }: GanttRowLabelsProps) {
  return (
    <div
      data-testid={ids.chartLeft}
      className="sticky left-0 z-10 shrink-0 border-r border-edge bg-panel text-[length:var(--text-sm)]"
      style={{ width: LEFT_WIDTH, paddingTop: AXIS_HEIGHT }}
    >
      {rows.map((row) => {
        const label = rowLabel(row);
        return (
          <div
            key={row.key}
            data-testid={ids.chartRow}
            data-key={row.kind === 'group' ? undefined : row.key}
            data-kind={row.kind}
            data-selected={row.key === selected ? true : undefined}
            className="flex items-center gap-2 overflow-hidden px-2 whitespace-nowrap"
            style={{
              height: ROW_HEIGHT,
              fontWeight: row.kind === 'bar' ? 400 : 600,
              background: row.key === selected ? 'var(--color-surface-2)' : undefined,
            }}
          >
            {label.key !== null && <span className="text-ink-muted">{label.key}</span>}
            <span className="truncate">{label.title}</span>
            {label.who !== '' && <span className="ml-auto text-ink-muted">{label.who}</span>}
          </div>
        );
      })}
    </div>
  );
}
