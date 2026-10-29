import type { FeatureBoardRowDto, FeatureBoardViewDto } from '../../../api/views';
import { TASKBOARD_VIEW } from './taskboardView';

// A leaf Feature whose stories and bugs are the Taskboard fixture's: two in the current sprint, one in the next, two in
// the Backlog (the closed ones start collapsed).
const SPRINT_24 = { id: 'sprint-24', name: 'Sprint 24', status: 'current' as const };
const SPRINT_25 = { id: 'sprint-25', name: 'Sprint 25', status: 'future' as const };
const SPRINT_OF = [SPRINT_24, SPRINT_24, SPRINT_25, null, null];
const ACTIVE = { id: 'fs-active', name: 'Active', category: 'inProgress' as const };

const rows: FeatureBoardRowDto[] = TASKBOARD_VIEW.rows.flatMap((row, i) =>
  row.parent === null
    ? []
    : [
        {
          parent: row.parent,
          offerResolve: row.offerResolve,
          cells: row.cells,
          featureId: 'f-checkout',
          sprint: SPRINT_OF[i] ?? null,
        },
      ],
);

export const FEATURE_BOARD_VIEW: FeatureBoardViewDto = {
  featureId: 'f-checkout',
  boardId: TASKBOARD_VIEW.boardId,
  header: {
    id: 'f-checkout',
    key: 'SHIP-101',
    title: 'Checkout v2',
    state: ACTIVE,
    featuresUnder: 0,
    storiesClosed: 2,
    storiesTotal: rows.length,
    pointsClosed: 2,
    pointsTotal: rows.reduce((n, r) => n + (r.parent.points ?? 0), 0),
    tasksClosed: 8,
    tasksTotal: rows.flatMap((r) => r.cells).flatMap((c) => c.tasks).length,
    sprints: [SPRINT_24, SPRINT_25],
    anyInBacklog: true,
  },
  columns: TASKBOARD_VIEW.columns,
  sections: [
    {
      id: 'f-checkout',
      key: 'SHIP-101',
      title: 'Checkout v2',
      state: ACTIVE,
      depth: 0,
      parentFeature: null,
      storiesClosed: 2,
      storiesTotal: rows.length,
      pointsClosed: 2,
      pointsTotal: rows.reduce((n, r) => n + (r.parent.points ?? 0), 0),
    },
  ],
  rows,
  peopleRows: TASKBOARD_VIEW.peopleRows,
  parentStates: TASKBOARD_VIEW.parentStates,
  styleRules: TASKBOARD_VIEW.styleRules,
  cardFields: TASKBOARD_VIEW.cardFields,
  annotations: TASKBOARD_VIEW.annotations,
};

// The same stories under a parent Feature: Reporting (SHIP-103) with two of its own, Dashboard widgets (SHIP-104) with the
// other three, and Chart types (SHIP-105) under that with none yet.
const OWN = rows.slice(0, 2).map((row) => ({ ...row, featureId: 'f-reporting' }));
const WIDGETS = rows.slice(2).map((row) => ({ ...row, featureId: 'f-widgets' }));
const counts = (list: FeatureBoardRowDto[]) => ({
  storiesClosed: list.filter((r) => r.parent.state.category === 'completed').length,
  storiesTotal: list.length,
  pointsClosed: list
    .filter((r) => r.parent.state.category === 'completed')
    .reduce((n, r) => n + (r.parent.points ?? 0), 0),
  pointsTotal: list.reduce((n, r) => n + (r.parent.points ?? 0), 0),
});
const section = (
  id: string,
  key: string,
  title: string,
  depth: number,
  list: FeatureBoardRowDto[],
) => ({
  id,
  key,
  title,
  state: ACTIVE,
  depth,
  parentFeature:
    depth < 2
      ? null
      : {
          id: 'f-widgets',
          key: 'SHIP-104',
          type: { id: 'feature' as const, name: 'Feature' },
          title: 'Dashboard widgets',
        },
  ...counts(list),
});

export const FEATURE_BOARD_PARENT_VIEW: FeatureBoardViewDto = {
  ...FEATURE_BOARD_VIEW,
  featureId: 'f-reporting',
  header: {
    ...FEATURE_BOARD_VIEW.header,
    id: 'f-reporting',
    key: 'SHIP-103',
    title: 'Reporting',
    featuresUnder: 2,
  },
  sections: [
    section('f-reporting', 'SHIP-103', 'Reporting', 0, OWN),
    section('f-widgets', 'SHIP-104', 'Dashboard widgets', 1, WIDGETS),
    section('f-charts', 'SHIP-105', 'Chart types', 2, []),
  ],
  rows: [...OWN, ...WIDGETS],
};
