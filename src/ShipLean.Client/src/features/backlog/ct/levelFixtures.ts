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

export function levelView(level: 'features' | 'epics', rows: BacklogRowDto[]): BacklogViewDto {
  return {
    projectId: PROJECT.id,
    level,
    mode: 'list',
    totalCount: rows.length,
    rows,
    sections: [],
    tagColours: [],
    forecast: null,
    velocity: null,
  };
}
