import { Chip } from '../../../../components/Chip';
import { StateDot } from '../../../../components/StateDot';
import { sprintsTestIds as ids } from '../../testIds';
import { RAIL_CATEGORIES, type RailSummary } from './railSummary';

const LABELS = {
  proposed: 'Proposed',
  inProgress: 'In progress',
  resolved: 'Resolved',
  completed: 'Completed',
} as const;

/** The sprint's story points grouped by state category, so custom states still roll up. */
export function PointsByState({ points }: { points: RailSummary['points'] }) {
  return (
    <section className="grid gap-1.5">
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Points by state
      </div>
      <div className="flex flex-wrap gap-1.5">
        {RAIL_CATEGORIES.map((category) => (
          <span key={category} data-testid={ids.railPoints} data-category={category}>
            <Chip tone={category === 'completed' ? 'ok' : 'muted'}>
              <StateDot category={category} name={`${LABELS[category]} ${points[category]}`} />
            </Chip>
          </span>
        ))}
      </div>
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Grouped by state category, so custom states still roll up.
      </p>
    </section>
  );
}

const CATEGORY_COLOURS = {
  proposed: 'var(--state-proposed)',
  inProgress: 'var(--state-in-progress)',
  resolved: 'var(--state-resolved)',
  completed: 'var(--state-completed)',
} as const;

/** The same points on a phone (SHIP-724 D7): one bar split by state category, with a legend under it. */
export function PointsBar({ points }: { points: RailSummary['points'] }) {
  const total = RAIL_CATEGORIES.reduce((sum, category) => sum + points[category], 0);
  return (
    <div className="grid gap-[5px]">
      <div
        data-testid={ids.pointsBar}
        role="img"
        aria-label="Points by state"
        className="flex h-2 overflow-hidden rounded-[4px] bg-panel-sunken"
      >
        {RAIL_CATEGORIES.map((category) =>
          points[category] > 0 ? (
            <i
              key={category}
              data-category={category}
              className="block h-full"
              style={{
                width: `${(points[category] / total) * 100}%`,
                background: CATEGORY_COLOURS[category],
              }}
            />
          ) : null,
        )}
      </div>
      <div className="flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-ink-muted">
        {RAIL_CATEGORIES.map((category) => (
          <span
            key={category}
            data-testid={ids.railPoints}
            data-category={category}
            className="inline-flex items-center gap-[5px] whitespace-nowrap"
          >
            <i
              aria-hidden="true"
              className="size-2 flex-none rounded-full"
              style={{ background: CATEGORY_COLOURS[category] }}
            />
            {LABELS[category]}{' '}
            <b className="font-semibold text-ink tabular-nums">{points[category]}</b>
          </span>
        ))}
      </div>
    </div>
  );
}
