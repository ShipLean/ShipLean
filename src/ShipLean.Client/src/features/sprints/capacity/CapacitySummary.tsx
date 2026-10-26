import { CapacityBar } from '../../../components/CapacityBar';
import { sprintsTestIds as ids } from '../testIds';
import { roundPoints } from './capacityDraft';
import { compareToVelocity } from './compareToVelocity';
import { VelocityCallout } from './VelocityCallout';

interface CapacitySummaryProps {
  sprintName: string;
  total: number;
  entered: boolean;
  committed: number;
  /** The project's override, else the average of the last sprints. */
  velocity: number;
  /** Whether `velocity` is the project's override rather than the average. */
  overridden: boolean;
  velocityWindow: number;
}

/** The sprint as a whole: team capacity (or velocity when none is entered) against what is committed. */
export function CapacitySummary({
  sprintName,
  total,
  entered,
  committed,
  velocity,
  overridden,
  velocityWindow,
}: CapacitySummaryProps) {
  const capacity = entered ? total : velocity;
  const over = committed > capacity;
  return (
    <aside
      aria-label={`${sprintName} as a whole`}
      className="grid content-start gap-3 border-t border-edge p-4 lg:w-80 lg:border-t-0 lg:border-l"
    >
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {sprintName} as a whole
      </div>
      <div className="flex gap-3">
        <Figure testId={ids.capacityKpiCapacity} value={capacity} label="Team capacity" />
        <Figure testId={ids.capacityKpiCommitted} value={committed} label="Committed" />
        <Figure
          testId={ids.capacityKpiVelocity}
          value={velocity}
          label={overridden ? 'Velocity (override)' : `Velocity (last ${velocityWindow})`}
        />
      </div>
      <CapacityBar
        used={committed}
        capacity={capacity}
        variant={entered ? 'capacity' : 'velocity'}
      />
      <div data-testid={ids.capacityRoom} data-over={over} className="text-[length:var(--text-sm)]">
        {over ? (
          <>
            <b className="text-alert">{roundPoints(committed - capacity)} pts over</b> what the team
            can take on. A warning only; nothing is blocked.
          </>
        ) : (
          <>
            <b>{roundPoints(capacity - committed)} pts</b> of room left in the sprint.
          </>
        )}
      </div>
      <VelocityCallout
        comparison={compareToVelocity(total, entered, velocity)}
        velocity={velocity}
        overridden={overridden}
      />
    </aside>
  );
}

function Figure({ testId, value, label }: { testId: string; value: number; label: string }) {
  return (
    <div className="min-w-0 flex-1">
      <div data-testid={testId} className="text-[length:var(--text-xl)] font-semibold tabular-nums">
        {value}
      </div>
      <div className="text-[length:var(--text-xs)] text-ink-muted">{label}</div>
    </div>
  );
}
