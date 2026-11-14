import { useNavigate, useSearchParams } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { RetroRange } from '@shiplean/core/api/retrosApi';
import { routePath } from '../../../../routes';
import { MoreButton } from '../../../../touch/MoreButton';
import type { SheetAction } from '../../../../touch/ActionSheet';
import { retrosTestIds as ids } from '../../testIds';
import { rangeLabel } from '../RangeSelect';
import { defsSheetValue, withMerge, withSheet } from './insightsAddress';

/**
 * Insights at 900px and below (SHIP-731 D2, D3): three equal tabs, then one row with Range ▾ and ⋯. Not sticky.
 * `merge` is the Merge themes button the desktop toolbar has, kept mounted because it opens the full-screen merge from
 * the address.
 */
export function InsightsTouchToolbar({
  projectKey,
  views,
  view,
  range,
  loaded,
  unthemedTotal,
  onView,
  merge,
}: {
  projectKey: string;
  views: readonly { id: string; short: string }[];
  view: string;
  range: RetroRange;
  // Whether the insights have loaded, for a test to wait on.
  loaded: boolean;
  unthemedTotal: number;
  onView: (view: string) => void;
  merge: ReactNode;
}) {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const write = (next: URLSearchParams) => setParams(next, { replace: true });
  const actions: SheetAction[] = [
    { id: 'merge', label: 'Merge themes…', onSelect: () => write(withMerge(params)) },
    {
      id: 'unthemed',
      label: `Give unthemed points themes (${unthemedTotal})`,
      disabled: unthemedTotal === 0,
      onSelect: () => void navigate(routePath('retros', { key: projectKey }, { unthemed: 'true' })),
    },
    {
      id: 'defs',
      label: 'What the statuses and verdicts mean',
      onSelect: () => write(withSheet(params, defsSheetValue())),
    },
    {
      id: 'search',
      label: 'Search retrospectives',
      onSelect: () => void navigate(routePath('retros', { key: projectKey })),
    },
    {
      id: 'features',
      label: 'Retrospectives by Feature',
      onSelect: () => void navigate(routePath('retroFeatures', { key: projectKey })),
    },
  ];
  return (
    <div data-testid={ids.retroInsightsToolbar} data-loaded={loaded} className="grid gap-2">
      <div
        role="group"
        aria-label="Insight"
        className="grid grid-cols-3 border-b border-edge bg-panel"
      >
        {views.map((v) => (
          <button
            key={v.id}
            type="button"
            data-testid={ids.retroInsightsTab}
            data-value={v.id}
            aria-pressed={view === v.id}
            className="h-10 min-w-0 cursor-pointer truncate border-b-2 border-transparent px-1.5 text-[length:var(--text-base)] text-ink-muted aria-pressed:border-signal aria-pressed:font-semibold aria-pressed:text-ink"
            onClick={() => onView(v.id)}
          >
            {v.short}
          </button>
        ))}
      </div>
      <div className="flex min-w-0 items-center gap-2">
        <button
          type="button"
          data-testid={ids.retroInsightsRange}
          aria-label="Range"
          aria-haspopup="dialog"
          className="inline-flex h-[var(--touch-target)] min-w-0 flex-none items-center gap-1 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink"
          onClick={() => write(withSheet(params, 'range'))}
        >
          <span className="truncate">{rangeLabel(range)}</span>
          <span aria-hidden="true" className="text-[length:var(--text-xs)] text-ink-muted">
            ▾
          </span>
        </button>
        <span className="flex-1" />
        {/* TODO: L731d replaces this hidden desktop button with the full-screen merge opened from `merge`. */}
        <span hidden>{merge}</span>
        <MoreButton label="Insights" actions={actions} />
      </div>
    </div>
  );
}
