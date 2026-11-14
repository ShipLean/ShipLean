import type { RetroFeatureRowDto } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';
import type { RetroFeaturesAddress } from '../retroFeaturesAddress';
import { CategoryLegend } from './CategoryLegend';
import { PhoneFeatureRow } from './PhoneFeatureRow';

function emptyText({ show, q }: RetroFeaturesAddress): string {
  if (q.trim()) return `No Feature matches “${q.trim()}”.`;
  return show === 'open' ? 'Nothing left to decide.' : 'Nothing here.';
}

/** The phone list: a head with the Feature count and the legend, one row link per Feature, then the stories and bugs with no Feature. */
export function PhoneFeatureList({
  projectKey,
  address,
  features,
  loose,
  onClearSearch,
}: {
  projectKey: string;
  address: RetroFeaturesAddress;
  features: RetroFeatureRowDto[];
  loose: RetroFeatureRowDto[];
  onClearSearch: () => void;
}) {
  const empty = features.length + loose.length === 0;
  return (
    <div
      data-testid={ids.retroFeaturesPhoneList}
      className="overflow-hidden rounded-panel border border-edge bg-panel"
    >
      <div
        data-testid={ids.retroFeaturesListHead}
        className="grid gap-1.5 border-b border-edge bg-sunken px-3 py-2"
      >
        <span
          data-testid={ids.retroFeaturesCount}
          className="text-[length:var(--text-sm)] font-semibold"
        >
          {features.length} Feature{features.length === 1 ? '' : 's'} with retros
        </span>
        <CategoryLegend />
      </div>
      {features.map((row) => (
        <PhoneFeatureRow key={row.item.id} projectKey={projectKey} row={row} />
      ))}
      {loose.length ? (
        <div
          data-testid={ids.retroFeaturesLoose}
          className="border-t border-edge bg-canvas px-3 py-1.5 text-[length:var(--text-xs)] font-semibold text-ink-muted"
        >
          Stories and bugs with no Feature
        </div>
      ) : null}
      {loose.map((row) => (
        <PhoneFeatureRow key={row.item.id} projectKey={projectKey} row={row} />
      ))}
      {empty ? (
        <div
          data-testid={ids.retroFeaturesEmpty}
          className="grid justify-items-center gap-2 px-3 py-7 text-center text-[length:var(--text-sm)] text-ink-muted"
        >
          <span>{emptyText(address)}</span>
          {address.q.trim() ? (
            <button
              type="button"
              data-testid={ids.retroFeaturesClearSearch}
              className="h-[var(--touch-target)] cursor-pointer rounded-control border border-edge bg-panel px-3 text-ink"
              onClick={onClearSearch}
            >
              Clear search
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
