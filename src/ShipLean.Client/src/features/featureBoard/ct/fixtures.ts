import type { FeatureBoardRowDto, FeatureBoardViewDto } from '../../../api/views';
import { VIEW } from '../../taskboard/ct/fixtures';

export const FEATURE_ID = 'f-checkout';
export const FEATURE_KEY = 'SHIP-100';
export const CURRENT = { id: 's24', name: 'Sprint 24', status: 'current' as const };
export const NEXT = { id: 's25', name: 'Sprint 25', status: 'future' as const };

// SHIP-141 (open, three tasks) is in the current sprint, SHIP-138 (closed) in the Backlog.
const [open, done] = VIEW.rows;
const rows: FeatureBoardRowDto[] = [
  {
    parent: open.parent!,
    offerResolve: false,
    cells: open.cells,
    featureId: FEATURE_ID,
    sprint: CURRENT,
  },
  {
    parent: done.parent!,
    offerResolve: false,
    cells: done.cells,
    featureId: FEATURE_ID,
    sprint: null,
  },
];

export const FEATURE_VIEW: FeatureBoardViewDto = {
  featureId: FEATURE_ID,
  boardId: VIEW.boardId,
  header: {
    id: FEATURE_ID,
    key: FEATURE_KEY,
    title: 'Checkout v2',
    state: { id: 'fs-active', name: 'Active', category: 'inProgress' },
    featuresUnder: 0,
    storiesClosed: 1,
    storiesTotal: 2,
    pointsClosed: 0,
    pointsTotal: 5,
    tasksClosed: 2,
    tasksTotal: 4,
    sprints: [CURRENT, NEXT],
    anyInBacklog: true,
  },
  columns: VIEW.columns,
  sections: [
    {
      id: FEATURE_ID,
      key: FEATURE_KEY,
      title: 'Checkout v2',
      state: { id: 'fs-active', name: 'Active', category: 'inProgress' },
      depth: 0,
      parentFeature: null,
      storiesClosed: 1,
      storiesTotal: 2,
      pointsClosed: 0,
      pointsTotal: 5,
    },
  ],
  rows,
  peopleRows: VIEW.peopleRows,
  parentStates: VIEW.parentStates,
  styleRules: [],
  cardFields: [],
  annotations: VIEW.annotations,
};
