// Inspection's lead-time read for a project (SHIP-1398 K3): one request per screen. The shape is in views.ts.
import { api } from './client';
import type { InspectionCut, InspectionMetricsViewDto, InspectionRange } from './views';

export interface InspectionMetricsQuery {
  range?: InspectionRange;
  cut?: InspectionCut;
  /** A sprint id of the project. */
  period?: string;
  /** A task key; only with a period. */
  select?: string;
}

export const inspectionMetricsKeys = {
  view: (projectId: string, query: InspectionMetricsQuery) =>
    ['projects', projectId, 'inspection-metrics', query] as const,
};

export const getInspectionMetrics = (projectId: string, query: InspectionMetricsQuery = {}) => {
  const params = new URLSearchParams();
  for (const name of ['range', 'cut', 'period', 'select'] as const) {
    const value = query[name];
    if (value) params.set(name, value);
  }
  const search = params.toString();
  return api.get<InspectionMetricsViewDto>(
    `/api/projects/${encodeURIComponent(projectId)}/inspection-metrics${search ? `?${search}` : ''}`,
  );
};
