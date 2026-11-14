import { RETRO_CATEGORIES, type RetroCategory } from '@shiplean/core/api/retros';
import type { WrittenBy } from './views/byCategory/writtenBy';

export type RetroView = 'category' | 'item';

/** What the sprint Retro tab keeps in its address: `?decide=open&point=<id>&view=item&writtenBy=agents&cat=wentBadly`. */
export interface SprintRetroAddress {
  /** True for `decide=open`: only the points still to decide. */
  decide: boolean;
  /** The point to highlight. */
  point: string | undefined;
  view: RetroView;
  writtenBy: WrittenBy;
  /** The category tile picked at touch; the page opens on Went well. */
  cat: RetroCategory;
}

const VIEWS: readonly RetroView[] = ['category', 'item'];
const WRITTEN_BY: readonly WrittenBy[] = ['all', 'agents', 'people'];

export function parseSprintRetroAddress(params: URLSearchParams): SprintRetroAddress {
  const view = params.get('view');
  const writtenBy = params.get('writtenBy');
  const cat = params.get('cat');
  return {
    decide: params.get('decide') === 'open',
    point: params.get('point') || undefined,
    view: VIEWS.find((v) => v === view) ?? 'category',
    writtenBy: WRITTEN_BY.find((w) => w === writtenBy) ?? 'all',
    cat: RETRO_CATEGORIES.find((c) => c === cat) ?? 'wentWell',
  };
}

/** The address with one value changed; a default value drops its parameter so the tab opens at a clean address. */
export function withSprintRetroValue(
  params: URLSearchParams,
  name: 'view' | 'writtenBy' | 'decide' | 'cat',
  value: RetroView | WrittenBy | RetroCategory | 'open' | 'all',
): URLSearchParams {
  const next = new URLSearchParams(params);
  const isDefault =
    value === 'all' ||
    (name === 'view' && value === 'category') ||
    (name === 'cat' && value === 'wentWell');
  if (isDefault) next.delete(name);
  else next.set(name, value);
  return next;
}
