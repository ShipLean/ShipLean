import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintCapacityDto } from '@shiplean/core/api/sprints';
import { routePath } from '../../../routes';
import { sprintsTestIds as ids } from '../testIds';

interface CapacityHeaderProps {
  project: ProjectDto;
  capacity: SprintCapacityDto;
  velocityText: string;
  /** The velocity text cannot be saved. */
  velocityInvalid: boolean;
  readOnly: boolean;
  /** Old points with no velocity: the box says one is needed to recompute them. */
  needsVelocity: boolean;
  onVelocity: (text: string) => void;
}

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** The top of the panel: the team's velocity for the sprint, and the calendar its days come from. */
export function CapacityHeader({
  project,
  capacity,
  velocityText,
  velocityInvalid,
  readOnly,
  needsVelocity,
  onVelocity,
}: CapacityHeaderProps) {
  const { workingDays, holidays, holidayCountry } = capacity;
  const names = holidays.map((h) => `${h.name} (${h.label})`).join(', ');
  return (
    <div className="grid gap-x-6 gap-y-3 border-b border-edge p-4 grid-cols-[auto_minmax(0,1fr)] touch:grid-cols-[minmax(0,1fr)] touch:p-3">
      <div className="grid gap-1 touch:grid-cols-[minmax(0,1fr)_6rem] touch:items-center touch:gap-x-3">
        {readOnly ? (
          <span
            id="capacity-velocity-label"
            className="text-[length:var(--text-sm)] font-medium text-ink touch:col-start-1 touch:row-start-1"
          >
            Team velocity
          </span>
        ) : (
          <label
            htmlFor="capacity-velocity"
            className="text-[length:var(--text-sm)] font-medium text-ink touch:col-start-1 touch:row-start-1"
          >
            Team velocity
          </label>
        )}
        {readOnly ? (
          <span
            role="group"
            aria-labelledby="capacity-velocity-label"
            data-testid={ids.capacityVelocity}
            className="h-7 text-[length:var(--text-lg)] leading-7 font-semibold tabular-nums touch:col-start-2 touch:row-span-2 touch:row-start-1 touch:text-right"
          >
            {capacity.velocity ?? '—'}
          </span>
        ) : (
          <input
            id="capacity-velocity"
            data-testid={ids.capacityVelocity}
            className="field w-28 text-right tabular-nums touch:col-start-2 touch:row-span-2 touch:row-start-1 touch:h-10 touch:w-24"
            type="number"
            min={0}
            max={9999.99}
            step="any"
            inputMode="decimal"
            aria-invalid={velocityInvalid}
            aria-describedby={`capacity-velocity-hint${velocityInvalid ? ' capacity-velocity-error' : ''}`}
            placeholder="—"
            value={velocityText}
            onChange={(event) => onVelocity(event.target.value)}
          />
        )}
        <span
          id="capacity-velocity-hint"
          className="text-[length:var(--text-xs)] text-ink-muted touch:col-start-1 touch:row-start-2"
        >
          {needsVelocity
            ? 'Enter a velocity to recompute these points'
            : 'Assumes everyone is in for every working day'}
        </span>
        {velocityInvalid ? (
          <span
            id="capacity-velocity-error"
            data-testid={ids.capacityVelocityError}
            className="text-[length:var(--text-xs)] text-alert touch:col-span-2"
          >
            Zero or more, up to 9999.99, two decimals at most.
          </span>
        ) : null}
      </div>
      <div className="grid content-start gap-1 text-[length:var(--text-sm)] touch:border-t touch:border-edge touch:pt-3">
        {holidayCountry === null ? (
          <p data-testid={ids.capacityNoCountry} className="m-0">
            {plural(workingDays, 'working day', 'working days')} · No public holidays: pick a
            country in{' '}
            {project.role === 'admin' ? (
              <Link
                data-testid={ids.capacityNoCountryLink}
                className="text-signal underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)] touch:items-center"
                to={routePath('projectSettings', { key: project.key, tab: 'general' })}
              >
                Settings › General
              </Link>
            ) : (
              'Settings › General'
            )}
          </p>
        ) : (
          <>
            <p data-testid={ids.capacityCalendar} className="m-0" title={names || undefined}>
              {plural(workingDays, 'working day', 'working days')} ·{' '}
              {plural(holidays.length, 'public holiday', 'public holidays')} ({holidayCountry})
            </p>
            {holidays.length === 0 ? null : (
              <p
                data-testid={ids.capacityHolidays}
                className="m-0 text-[length:var(--text-xs)] text-ink-muted"
              >
                {names}
              </p>
            )}
          </>
        )}
      </div>
    </div>
  );
}
