import type { FeatureBoardRowDto, FeatureBoardViewDto } from '../../../api/views';
import { PRIYA, TOM, VIEW, task } from '../../taskboard/ct/fixtures';

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

// A parent Feature: Reporting (SHIP-103) with its own story, Dashboard widgets (SHIP-104) under it with one story, and
// Chart types (SHIP-105) under that with none.
export const PARENT_KEY = 'SHIP-103';
const ACTIVE_FEATURE = { id: 'fs-active', name: 'Active', category: 'inProgress' as const };
const [own] = FEATURE_VIEW.rows;
const WIDGET_STORY = {
  ...own.parent,
  id: 'id-SHIP-150',
  key: 'SHIP-150',
  title: 'Resize widgets',
  rollups: [{ type: 'task' as const, open: 1, closed: 0 }],
};
const WIDGET_TASK = task(
  'SHIP-190',
  { id: 't-new', name: 'New', category: 'proposed' },
  {
    title: 'Drag handle',
    assignee: PRIYA,
    parent: {
      id: WIDGET_STORY.id,
      key: WIDGET_STORY.key,
      type: WIDGET_STORY.type,
      title: WIDGET_STORY.title,
    },
  },
);
const widgetRow: FeatureBoardRowDto = {
  parent: WIDGET_STORY,
  offerResolve: false,
  cells: own.cells.map((c) => ({ ...c, tasks: c.stateId === 't-new' ? [WIDGET_TASK] : [] })),
  featureId: 'f-widgets',
  sprint: null,
};
const section = (id: string, key: string, title: string, depth: number) => ({
  id,
  key,
  title,
  state: ACTIVE_FEATURE,
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
  storiesClosed: 0,
  storiesTotal: 0,
  pointsClosed: null,
  pointsTotal: null,
});

const allCards = [...own.cells.flatMap((c) => c.tasks), WIDGET_TASK];

export const PARENT_VIEW: FeatureBoardViewDto = {
  ...FEATURE_VIEW,
  featureId: 'f-reporting',
  header: {
    ...FEATURE_VIEW.header,
    id: 'f-reporting',
    key: PARENT_KEY,
    title: 'Reporting',
    featuresUnder: 2,
  },
  sections: [
    {
      ...section('f-reporting', PARENT_KEY, 'Reporting', 0),
      storiesClosed: 0,
      storiesTotal: 1,
      pointsClosed: 0,
      pointsTotal: 5,
    },
    {
      ...section('f-widgets', 'SHIP-104', 'Dashboard widgets', 1),
      storiesTotal: 1,
      pointsClosed: 0,
      pointsTotal: 5,
    },
    section('f-charts', 'SHIP-105', 'Chart types', 2),
  ],
  rows: [{ ...own, featureId: 'f-reporting', sprint: CURRENT }, widgetRow],
  peopleRows: [null, PRIYA, TOM].map((person) => {
    const mine = allCards.filter((c) => (c.assignee?.id ?? null) === (person?.id ?? null));
    return {
      person,
      taskCount: mine.length,
      cells: own.cells.map((c) => ({
        stateId: c.stateId,
        tasks: mine.filter((t) => t.state.id === c.stateId),
      })),
    };
  }),
};
