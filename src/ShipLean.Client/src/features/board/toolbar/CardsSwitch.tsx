import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { boardTestIds as ids } from '../testIds';

const OPTIONS = [
  { id: 'full', label: 'Full cards' },
  { id: 'compact', label: 'Compact' },
] as const;

// Full or compact cards (a per-user choice, saved by the board). In the touch More sheet (`onPicked` given) the two are
// rows, and picking one applies it and closes the sheet.
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
      <div role="group" aria-label="Cards" className="grid gap-1">
        {OPTIONS.map((option) => {
          const chosen = compact === (option.id === 'compact');
          return (
            <button
              key={option.id}
              type="button"
              data-testid={ids.cardsOption}
              data-option={option.id}
              aria-pressed={chosen}
              className={`${actionSheetRowClass} gap-2 ${chosen ? 'bg-signal-soft font-medium' : ''} text-ink`}
              onClick={() => {
                onCompact(option.id === 'compact');
                onPicked();
              }}
            >
              <span className="flex-1">{option.label}</span>
              {chosen ? <span aria-hidden="true">✓</span> : null}
            </button>
          );
        })}
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
