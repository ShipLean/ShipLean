import type { RetroFeatureRowDto, RetroFeaturesDto } from '../../../../api/retros';

const inProgress = {
  id: 'state-in-progress',
  name: 'In Progress',
  category: 'inProgress',
} as const;

type RowOptions = {
  id: string;
  key: string;
  title: string;
  type?: 'feature' | 'story' | 'bug';
  toDecide?: number;
  noFeatureRetro?: boolean;
};

/** A row in the DTO's shape: counts 4, 1, 2, 3 and `toDecide` of the 5 decidable points still to decide. */
export function featureRow({
  id,
  key,
  title,
  type = 'feature',
  toDecide = 0,
  noFeatureRetro = false,
}: RowOptions): RetroFeatureRowDto {
  return {
    item: { id, key, type: { id: type, name: type }, title },
    state: inProgress,
    retros: 3,
    ownRetros: noFeatureRetro ? 0 : 1,
    totals: {
      counts: { wentWell: 4, notInPlan: 1, wentBadly: 2, improve: 3 },
      toDecide,
      decided: 5 - toDecide,
      decidable: 5,
      actions: 2,
    },
    lastRetroLabel: 'yesterday',
    noFeatureRetro,
  };
}

/** Three Features (one with nothing to decide, one with no retro of its own) and a loose bug. */
export const sampleFeatures: RetroFeaturesDto = {
  features: [
    featureRow({ id: 'f1', key: 'SHIP-180', title: 'Saved cards', toDecide: 2 }),
    featureRow({ id: 'f2', key: 'SHIP-181', title: 'Receipts', toDecide: 0, noFeatureRetro: true }),
    featureRow({ id: 'f3', key: 'SHIP-182', title: 'Mobile sign-in', toDecide: 5 }),
  ],
  loose: [
    featureRow({ id: 'b1', key: 'SHIP-199', title: 'Favicon 404', type: 'bug', toDecide: 1 }),
  ],
};
