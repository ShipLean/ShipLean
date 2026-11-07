import type { ParentRefDto } from '../../../api/workItems';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { itemRow, PROJECT } from './fixtures';

const epic = (key: string, title: string): ParentRefDto => ({
  id: `id-${key}`,
  key,
  type: { id: 'epic', name: 'Epic' },
  title,
});
const feature = (key: string, title: string): ParentRefDto => ({
  id: `id-${key}`,
  key,
  type: { id: 'feature', name: 'Feature' },
  title,
});
const FEATURE_TYPE = { id: 'feature', name: 'Feature' } as const;
const EPIC_TYPE = { id: 'epic', name: 'Epic' } as const;
const ACTIVE = { id: 's-active', name: 'Active', category: 'inProgress' } as const;

/** Checkout v2 (sum warns), Reporting with its nested Dashboard widgets, and a Feature nobody estimated. */
export const FEATURE_ROWS: BacklogRowDto[] = [
  itemRow({
    key: 'SHIP-101',
    rank: 1,
    type: FEATURE_TYPE,
    title: 'Checkout v2',
    state: ACTIVE,
    points: 20,
    childPoints: 26,
    sumWarn: true,
    parent: epic('SHIP-90', 'Checkout conversion'),
    rollups: [
      { type: 'story', open: 3, closed: 3 },
      { type: 'bug', open: 1, closed: 1 },
    ],
  }),
  itemRow({
    key: 'SHIP-103',
    rank: 3,
    type: FEATURE_TYPE,
    title: 'Reporting',
    state: ACTIVE,
    points: 13,
    childPoints: 13,
    parent: epic('SHIP-92', 'Insights for merchants'),
    rollups: [
      { type: 'story', open: 4, closed: 0 },
      { type: 'feature', open: 1, closed: 0 },
    ],
  }),
  itemRow({
    key: 'SHIP-108',
    rank: 4,
    type: FEATURE_TYPE,
    title: 'Dashboard widgets',
    points: null,
    childPoints: null,
    parent: feature('SHIP-103', 'Reporting'),
    rollups: [],
  }),
];

export const EPIC_ROWS: BacklogRowDto[] = [
  itemRow({
    key: 'SHIP-92',
    rank: 3,
    type: EPIC_TYPE,
    title: 'Insights for merchants',
    points: 30,
    childPoints: 28,
    rollups: [
      { type: 'story', open: 4, closed: 0 },
      { type: 'feature', open: 2, closed: 0 },
    ],
  }),
];

/**
 * The Features tree: Epic SHIP-92 holding Reporting (SHIP-103) and its nested Dashboard widgets (SHIP-108), Epic SHIP-90 holding
 * Checkout v2, then No parent holding Ops hygiene (SHIP-104).
 */
export const FEATURE_TREE_ROWS: BacklogRowDto[] = [
  itemRow({ ...EPIC_ROWS[0], kind: 'group', rank: null }),
  { ...FEATURE_ROWS[1], depth: 1 },
  { ...FEATURE_ROWS[2], depth: 2 },
  itemRow({
    key: 'SHIP-90',
    kind: 'group',
    rank: null,
    type: EPIC_TYPE,
    title: 'Checkout conversion',
    points: 40,
  }),
  { ...FEATURE_ROWS[0], depth: 1 },
  itemRow({
    key: '',
    id: '00000000-0000-0000-0000-000000000000',
    kind: 'noParentGroup',
    type: null,
    title: 'No parent',
    state: null,
    points: null,
    rank: null,
    rollups: [{ type: 'feature', open: 1, closed: 0 }],
  }),
  itemRow({
    key: 'SHIP-104',
    rank: 5,
    depth: 1,
    type: FEATURE_TYPE,
    title: 'Ops hygiene',
    points: 5,
  }),
];

export function levelView(
  level: 'features' | 'epics',
  rows: BacklogRowDto[],
  mode: 'list' | 'tree' = 'list',
): BacklogViewDto {
  return {
    projectId: PROJECT.id,
    level,
    mode,
    totalCount: rows.length,
    rows,
    sections: [],
    tagColours: [],
    forecast: null,
    velocity: null,
    review: null,
  };
}
