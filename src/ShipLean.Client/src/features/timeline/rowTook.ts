import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import type { TimelineRow } from './chart/timelineRows';

/** What a row's TOOK cell says: the text, and how it is drawn. */
export interface RowTook {
  text: string;
  /** An open bar: drawn in the accent colour. */
  open?: boolean;
  /** Not started story or bug: drawn in italics. */
  italic?: boolean;
  title?: string;
}

/** The TOOK cell of a row, from the server's labels. */
export function rowTook(row: TimelineRow, view: FeatureTimelineViewDto): RowTook {
  switch (row.kind) {
    case 'bracket':
      return { text: row.story.durationLabel ?? '' };
    case 'bar':
      if (row.bar.inferred) return { text: 'No start' };
      if (row.bar.state === 'inFlight')
        return {
          text: `${row.bar.durationLabel ?? ''} ▸`,
          open: true,
          title: 'Still open: so far',
        };
      return { text: row.bar.durationLabel ?? '' };
    case 'notStarted':
      return {
        text: 'Not started',
        italic: view.notStarted.some((i) => i.key === row.key && i.type !== 'task'),
      };
    case 'group':
      return { text: row.tasks === 1 ? '1 task' : `${row.tasks} tasks` };
  }
}
