import type { InspectionMetricsQuery } from '@shiplean/core/api/inspectionMetrics';
import {
  INSPECTION_CUTS,
  INSPECTION_RANGES,
  type InspectionCut,
  type InspectionRange,
} from '@shiplean/core/api/views';

// Inspection's address (SHIP-1398 K10): /p/:key/inspection?view=&range=&cut=&p=&select=&tab=. A missing or unknown value
// reads as its default, and the default is left out of the address again.
export const INSPECTION_VIEWS = ['overview', 'leadtime', 'period', 'json'] as const;
export type InspectionView = (typeof INSPECTION_VIEWS)[number];

export const INSPECTION_PERIOD_TABS = ['tasks', 'features'] as const;
export type InspectionPeriodTab = (typeof INSPECTION_PERIOD_TABS)[number];

export interface InspectionQuery {
  view: InspectionView;
  range: InspectionRange;
  cut: InspectionCut;
  /** The sprint open on the period view (its id), from `p`. */
  period: string | null;
  /** The task selected in that sprint (its key). */
  select: string | null;
  tab: InspectionPeriodTab;
}

export const DEFAULT_INSPECTION_QUERY: InspectionQuery = {
  view: 'overview',
  range: 'last6',
  cut: 'none',
  period: null,
  select: null,
  tab: 'tasks',
};

const oneOf = <T extends string>(values: readonly T[], value: string | null, fallback: T): T =>
  values.find((v) => v === value) ?? fallback;

const text = (value: string | null) => (value === null || value === '' ? null : value);

export function queryFromSearch(params: URLSearchParams): InspectionQuery {
  const d = DEFAULT_INSPECTION_QUERY;
  return {
    view: oneOf(INSPECTION_VIEWS, params.get('view'), d.view),
    range: oneOf(INSPECTION_RANGES, params.get('range'), d.range),
    cut: oneOf(INSPECTION_CUTS, params.get('cut'), d.cut),
    period: text(params.get('p')),
    select: text(params.get('select')),
    tab: oneOf(INSPECTION_PERIOD_TABS, params.get('tab'), d.tab),
  };
}

export function queryToSearch(query: InspectionQuery): URLSearchParams {
  const d = DEFAULT_INSPECTION_QUERY;
  const params = new URLSearchParams();
  if (query.view !== d.view) params.set('view', query.view);
  if (query.range !== d.range) params.set('range', query.range);
  if (query.cut !== d.cut) params.set('cut', query.cut);
  if (query.period !== null) params.set('p', query.period);
  if (query.select !== null) params.set('select', query.select);
  if (query.tab !== d.tab) params.set('tab', query.tab);
  return params;
}

// The one read of the screen: a sprint (and a task in it) only on the period view, since `select` without `period` is a
// validation error on the server.
export function apiQueryOf(query: InspectionQuery): InspectionMetricsQuery {
  const period = query.view === 'period' && query.period !== null ? query.period : undefined;
  return {
    range: query.range,
    cut: query.cut,
    ...(period === undefined ? {} : { period }),
    ...(period !== undefined && query.select !== null ? { select: query.select } : {}),
  };
}
