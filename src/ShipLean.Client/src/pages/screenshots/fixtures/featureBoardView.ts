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
