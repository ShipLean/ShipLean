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
