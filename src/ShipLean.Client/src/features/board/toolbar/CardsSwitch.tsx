import { boardTestIds as ids } from '../testIds';

const OPTIONS = [
  { id: 'full', label: 'Full cards' },
  { id: 'compact', label: 'Compact' },
] as const;

// Full or compact cards (a per-user choice, saved by the board).
export function CardsSwitch({
  compact,
  onCompact,
}: {
  compact: boolean;
  onCompact: (compact: boolean) => void;
}) {
  return (
    <div
      role="group"
      aria-label="Cards"
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {OPTIONS.map((option) => (
        <button
          key={option.id}
          type="button"
          data-testid={ids.cardsOption}
          data-option={option.id}
          aria-pressed={compact === (option.id === 'compact')}
          className={`h-7 px-3 font-medium ${
            compact === (option.id === 'compact')
              ? 'bg-signal-soft text-ink'
              : 'text-ink-muted hover:text-ink'
          }`}
          onClick={() => onCompact(option.id === 'compact')}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
