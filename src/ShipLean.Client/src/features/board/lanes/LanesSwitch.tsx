import { actionSheetRowClass } from '../../../touch/ActionSheet';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';

const hintClass = 'text-[length:var(--text-xs)] text-ink-muted';

const FEATURE_OPTIONS = [
  { id: 'none', label: 'None' },
  { id: 'on', label: 'Swimlanes' },
  { id: 'epic', label: 'By Epic' },
] as const;

/** The Features board's lane choice from the saved value; no saved choice is swimlanes on, as on Stories. */
export function featuresLaneChoice(value: string | null): 'none' | 'on' | 'epic' {
  if (value === 'epic') return 'epic';
  return value === 'none' || value === 'off' ? 'none' : 'on';
}

// The lane choice of the signed-in user. Stories is a checkbox: swimlanes on, or off. Features is three ways: one lane,
// the board's stored swimlanes (the default), or one lane per Epic. The Epics board has no lanes to choose. A Stories
// board that shows one lane and has swimlanes on has no stored lanes: the toggle would change nothing, so it is left
// out (it stays when the user has turned lanes off, to turn them on).
// In the touch More sheet (`onPicked` given) the controls are rows: Stories keeps the sheet open on the checkbox, Features
// applies the picked row and closes the sheet.
export function LanesSwitch({
  level,
  laneCount,
  value,
  onChoose,
  onPicked,
  laneNames = [],
}: {
  level: Level;
  /** The lanes the board shows now. */
  laneCount: number;
  value: string | null;
  onChoose: (value: string) => void;
  onPicked?: () => void;
  /** The lanes' names, top first; in the touch sheet they say which lane sits above which. */
  laneNames?: string[];
}) {
  const hint = laneNames.length > 1 ? laneNames.join(' above ') : null;
  if (level === 'stories') {
    if (laneCount <= 1 && value !== 'off') return null;
    return (
      <label
        className={
          onPicked
            ? `${actionSheetRowClass} flex-row-reverse gap-3 text-ink`
            : 'inline-flex items-center gap-1.5 text-ink-muted'
        }
      >
        <input
          type="checkbox"
          data-testid={ids.lanesToggle}
          checked={value !== 'off'}
          onChange={(event) => onChoose(event.target.checked ? 'on' : 'off')}
        />
        {onPicked ? (
          <span className="flex min-w-0 flex-1 flex-col">
            <span>Swimlanes</span>
            {hint === null ? null : (
              <span data-testid={ids.lanesHint} className={hintClass}>
                {hint}
              </span>
            )}
          </span>
        ) : (
          'Swimlanes'
        )}
      </label>
    );
  }
  if (level === 'epics') return null;
  // "none" and "off" are the one-lane view.
  const choice = featuresLaneChoice(value);
  if (onPicked) {
    return (
      <div role="group" aria-label="Lanes" className="grid gap-1">
        {FEATURE_OPTIONS.map((option) => (
          <button
            key={option.id}
            type="button"
            data-testid={ids.lanesOption}
            data-option={option.id}
            aria-pressed={choice === option.id}
            className={`${actionSheetRowClass} gap-2 ${choice === option.id ? 'bg-signal-soft font-medium' : ''} text-ink`}
            onClick={() => {
              onChoose(option.id);
              onPicked();
            }}
          >
            <span className="flex min-w-0 flex-1 flex-col">
              <span>{option.label}</span>
              {option.id === 'on' && hint !== null ? (
                <span data-testid={ids.lanesHint} className={hintClass}>
                  {hint}
                </span>
              ) : null}
            </span>
            {choice === option.id ? <span aria-hidden="true">✓</span> : null}
          </button>
        ))}
      </div>
    );
  }
  return (
    <div
      role="group"
      aria-label="Lanes"
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {FEATURE_OPTIONS.map((option) => (
        <button
          key={option.id}
          type="button"
          data-testid={ids.lanesOption}
          data-option={option.id}
          aria-pressed={choice === option.id}
          className={`h-7 px-3 font-medium ${
            choice === option.id ? 'bg-signal-soft text-ink' : 'text-ink-muted hover:text-ink'
          }`}
          onClick={() => onChoose(option.id)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
