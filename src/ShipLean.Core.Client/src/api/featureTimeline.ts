// The Feature timeline read for one Feature, by key (C2). The shape is in views.ts.
import { api } from './client';
import type { FeatureTimelineViewDto } from './views';

export const featureTimelineKeys = {
  timeline: (projectId: string, featureKey: string) =>
    ['projects', projectId, 'feature-timeline', 'by-key', featureKey] as const,
};

export const getFeatureTimeline = (projectId: string, featureKey: string) =>
  api.get<FeatureTimelineViewDto>(
    `/api/projects/${encodeURIComponent(projectId)}/work-items/by-key/${encodeURIComponent(featureKey)}/timeline`,
  );
