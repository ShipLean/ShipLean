import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { boardTestIds as ids } from '../testIds';

const OPTIONS = [
  { id: 'full', label: 'Full cards', short: 'Full' },
  { id: 'compact', label: 'Compact', short: 'Compact' },
] as const;

// Full or compact cards (a per-user choice, saved by the board). In the touch More sheet (`onPicked` given) it is one row,
// "Cards" with a Full | Compact segmented control, and picking one applies it and closes the sheet.
export function CardsSwitch({
  compact,
  onCompact,
  onPicked,
}: {
  compact: boolean;
  onCompact: (compact: boolean) => void;
  onPicked?: () => void;
}) {
  if (onPicked) {
    return (
      <div className={`${actionSheetRowClass} justify-between gap-3 text-ink`}>
        <span id="board-cards-label">Cards</span>
        <div
          role="group"
          aria-labelledby="board-cards-label"
          className="inline-flex overflow-hidden rounded-control border border-edge"
        >
          {OPTIONS.map((option) => {
            const chosen = compact === (option.id === 'compact');
            return (
              <button
                key={option.id}
                type="button"
                data-testid={ids.cardsOption}
                data-option={option.id}
                aria-pressed={chosen}
                className={`min-h-[var(--touch-target)] px-4 font-medium ${chosen ? 'bg-signal-soft text-ink' : 'text-ink-muted'}`}
                onClick={() => {
                  onCompact(option.id === 'compact');
                  onPicked();
                }}
              >
                {option.short}
              </button>
            );
          })}
        </div>
      </div>
    );
  }
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
