import { useState } from 'react';
import { retrosTestIds as ids } from '../../testIds';
import { SHOWS } from '../FeaturesToolbar';
import type { RetroFeaturesAddress, RetroFeaturesShow } from '../retroFeaturesAddress';

/** One row: the Show filter and a Search button. Search turns the row into a field with Cancel, and Cancel clears it. */
export function PhoneFeaturesToolbar({
  address,
  onShow,
  onSearch,
}: {
  address: RetroFeaturesAddress;
  onShow: (show: RetroFeaturesShow) => void;
  onSearch: (q: string) => void;
}) {
  const [searching, setSearching] = useState(address.q !== '');
  if (searching) {
    return (
      <div className="flex items-center gap-2">
        <input
          type="text"
          inputMode="search"
          enterKeyHint="search"
          autoComplete="off"
          autoFocus
          data-testid={ids.retroFeaturesSearch}
          aria-label="Search Features"
          placeholder="Search Features"
          value={address.q}
          className="h-[var(--touch-target)] min-w-0 flex-1 rounded-control border border-edge bg-panel px-2.5 text-ink placeholder:text-ink-muted focus:ring-1 focus:ring-signal focus:outline-none"
          onChange={(event) => onSearch(event.target.value)}
        />
        <button
          type="button"
          data-testid={ids.retroFeaturesSearchCancel}
          className="h-[var(--touch-target)] cursor-pointer px-1 text-[length:var(--text-sm)] text-signal"
          onClick={() => {
            onSearch('');
            setSearching(false);
          }}
        >
          Cancel
        </button>
      </div>
    );
  }
  return (
    <div className="flex items-center gap-2">
      <div
        role="group"
        aria-label="Show"
        data-testid={ids.retroFeaturesFilter}
        className="inline-flex min-w-0 overflow-hidden rounded-control border border-edge"
      >
        {SHOWS.map((option) => (
          <button
            key={option.id}
            type="button"
            data-testid={ids.retroFeaturesFilterOption}
            data-value={option.id}
            aria-pressed={address.show === option.id}
            className="h-[var(--touch-target)] cursor-pointer border-r border-edge bg-panel px-2.5 text-[length:var(--text-sm)] whitespace-nowrap text-ink-muted last:border-r-0 aria-pressed:bg-signal-soft aria-pressed:font-semibold aria-pressed:text-ink"
            onClick={() => onShow(option.id)}
          >
            {option.label}
          </button>
        ))}
      </div>
      <span className="flex-1" />
      <button
        type="button"
        data-testid={ids.retroFeaturesSearchOpen}
        aria-pressed={address.q !== ''}
        className="inline-flex h-[var(--touch-target)] flex-none cursor-pointer items-center gap-1.5 rounded-control border border-edge bg-panel px-2.5 text-[length:var(--text-sm)] aria-pressed:border-signal aria-pressed:bg-signal-soft aria-pressed:text-signal"
        onClick={() => setSearching(true)}
      >
        <svg
          viewBox="0 0 16 16"
          data-testid={ids.retroFeaturesSearchIcon}
          className="size-3.5"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.6"
          aria-hidden="true"
        >
          <circle cx="7" cy="7" r="4.5" />
          <path d="M10.5 10.5 14 14" strokeLinecap="round" />
        </svg>
        Search
      </button>
    </div>
  );
}
