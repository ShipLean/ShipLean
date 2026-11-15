import type { VelocityDto } from '@shiplean/core/api/sprints';
import type { BacklogSectionDto } from '@shiplean/core/api/views';
import { backlogTestIds as ids } from '../../testIds';
import { sectionDropId } from '../planningTarget';
import { SectionToggle } from '../SectionToggle';
import type { SectionCollapse } from '../useSectionCollapse';
import type { LitSection } from './useTouchPlanningDrop';

/**
 * The Backlog section's header at touch: its items and points, and with Forecast on the velocity it plans at (the
 * override itself is in ⋯ More). A row dropped on it leaves its sprint.
 */
export function TouchBacklogBlock({
  section,
  velocity,
  forecast,
  collapse,
  lit,
}: {
  section: BacklogSectionDto;
  velocity: VelocityDto | null;
  forecast: boolean;
  collapse: SectionCollapse;
  lit: LitSection | null;
}) {
  const planned = velocity === null ? null : (velocity.override ?? velocity.average);
  const recent = velocity?.sprints.map((s) => s.completed).join(' · ') ?? '';
  return (
    <div
      data-testid={ids.planningSection}
      data-section={section.id}
      data-drop-target={sectionDropId(section.id)}
      data-over={lit !== null}
      className={`border-y border-edge px-2 py-2 ${lit === null ? 'bg-signal-soft' : 'bg-accent-soft outline-2 -outline-offset-2 outline-signal'}`}
    >
      <div className="flex items-center gap-1">
        <SectionToggle
          title={section.title}
          open={collapse.isOpen(section.id)}
          disabled={collapse.searching}
          onToggle={() => collapse.toggle(section.id)}
        />
        <b data-testid={ids.planningSectionTitle} className="text-[length:var(--text-md)]">
          {section.title}
        </b>
        <span
          data-testid={ids.planningSectionCount}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          {section.rows.length} items · {section.points} pts
        </span>
      </div>
      {forecast && velocity !== null && planned !== null ? (
        <p
          data-testid={ids.planningVelocity}
          data-overridden={velocity.override !== null}
          className="pl-[var(--touch-target)] text-[length:var(--text-sm)]"
        >
          Forecast at velocity <b>{planned}</b>{' '}
          <span className="text-ink-muted">
            {velocity.override !== null
              ? '(override)'
              : `(average of last ${velocity.sprints.length} sprints${recent === '' ? '' : `: ${recent}`})`}
          </span>
        </p>
      ) : null}
      {lit === null ? null : (
        <p
          data-testid={ids.planningDropMessage}
          className="pl-[var(--touch-target)] pt-1 text-[length:var(--text-sm)] font-semibold text-accent"
        >
          {lit.message}
        </p>
      )}
    </div>
  );
}
