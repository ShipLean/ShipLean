import { retrosTestIds as ids } from '../testIds';
import type { RetroFeaturesAddress, RetroFeaturesShow } from './retroFeaturesAddress';

const SHOWS: { id: RetroFeaturesShow; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'open', label: 'To decide' },
  { id: 'waiting', label: 'No Feature retro yet' },
];

/** The All / To decide / No Feature retro yet filter, the search box and how many Features are in view. */
export function FeaturesToolbar({
  address,
  featureCount,
  onShow,
  onSearch,
}: {
  address: RetroFeaturesAddress;
  featureCount: number;
  onShow: (show: RetroFeaturesShow) => void;
  onSearch: (q: string) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <div
        role="group"
        aria-label="Show"
        data-testid={ids.retroFeaturesFilter}
        className="inline-flex overflow-hidden rounded-control border border-edge"
      >
        {SHOWS.map((option) => (
          <button
            key={option.id}
            type="button"
            data-value={option.id}
            aria-pressed={address.show === option.id}
            className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] whitespace-nowrap text-ink-muted last:border-r-0 aria-pressed:bg-signal-soft aria-pressed:font-semibold aria-pressed:text-ink"
            onClick={() => onShow(option.id)}
          >
            {option.label}
          </button>
        ))}
      </div>
      <input
        type="search"
        data-testid={ids.retroFeaturesSearch}
        aria-label="Search Features"
        placeholder="Search Features"
        value={address.q}
        className="w-60 rounded-control border border-edge bg-panel px-2.5 py-1 text-[length:var(--text-sm)]"
        onChange={(event) => onSearch(event.target.value)}
      />
      <span className="flex-1" />
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        {featureCount} Feature{featureCount === 1 ? '' : 's'} with retros
      </span>
    </div>
  );
}
