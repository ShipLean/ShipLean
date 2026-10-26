import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';

const FEATURE_OPTIONS = [
  { id: 'none', label: 'None' },
  { id: 'epic', label: 'By Epic' },
] as const;

// The lane choice of the signed-in user: swimlanes on or off on Stories, no lanes or one lane per Epic on Features.
// The Epics board has no lanes to choose. A Stories board that shows one lane and has swimlanes on has no stored lanes:
// the toggle would change nothing, so it is left out (it stays when the user has turned lanes off, to turn them on).
export function LanesSwitch({
  level,
  laneCount,
  value,
  onChoose,
}: {
  level: Level;
  /** The lanes the board shows now. */
  laneCount: number;
  value: string | null;
  onChoose: (value: string) => void;
}) {
  if (level === 'stories') {
    if (laneCount <= 1 && value !== 'off') return null;
    return (
      <label className="inline-flex items-center gap-1.5 text-ink-muted">
        <input
          type="checkbox"
          data-testid={ids.lanesToggle}
          checked={value !== 'off'}
          onChange={(event) => onChoose(event.target.checked ? 'on' : 'off')}
        />
        Swimlanes
      </label>
    );
  }
  if (level === 'epics') return null;
  const byEpic = value === 'epic';
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
          aria-pressed={byEpic === (option.id === 'epic')}
          className={`h-7 px-3 font-medium ${
            byEpic === (option.id === 'epic')
              ? 'bg-signal-soft text-ink'
              : 'text-ink-muted hover:text-ink'
          }`}
          onClick={() => onChoose(option.id)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
