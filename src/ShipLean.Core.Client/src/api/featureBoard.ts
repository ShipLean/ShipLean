// The Feature board read for one Feature, by key. The shape is in views.ts.
import { api } from './client';
import type { FeatureBoardViewDto } from './views';

export const featureBoardKeys = {
  board: (projectId: string, featureKey: string) =>
    ['projects', projectId, 'feature-board', 'by-key', featureKey] as const,
};

export const getFeatureBoard = (projectId: string, featureKey: string) =>
  api.get<FeatureBoardViewDto>(
    `/api/projects/${encodeURIComponent(projectId)}/work-items/by-key/${encodeURIComponent(featureKey)}/feature-board`,
  );
