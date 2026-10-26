import type { Level } from '../../../routes';
import type { ColumnSpec } from '../backlogApi';

export type LevelColumnId =
  'rank' | 'key' | 'title' | 'parent' | 'state' | 'features' | 'points' | 'progress';

const lead: ColumnSpec[] = [
  { id: 'rank', label: '#', required: true },
  { id: 'key', label: 'Key', required: true },
  { id: 'title', label: 'Title', required: true },
];

/** The Features level's columns, in order. */
export const FEATURE_COLUMNS: readonly ColumnSpec[] = [
  ...lead,
  { id: 'parent', label: 'Parent', required: false },
  { id: 'state', label: 'State', required: false },
  { id: 'points', label: 'Pts', required: false },
  { id: 'progress', label: 'Progress (stories and bugs)', required: false },
];

/** The Epics level's columns, in order: a Features count in place of Parent. */
export const EPIC_COLUMNS: readonly ColumnSpec[] = [
  ...lead,
  { id: 'state', label: 'State', required: false },
  { id: 'features', label: 'Features', required: false },
  { id: 'points', label: 'Pts', required: false },
  { id: 'progress', label: 'Progress (stories and bugs)', required: false },
];

export type LevelBacklogLevel = Exclude<Level, 'stories'>;

export const columnsOf = (level: LevelBacklogLevel): readonly ColumnSpec[] =>
  level === 'epics' ? EPIC_COLUMNS : FEATURE_COLUMNS;
