import { Link } from 'react-router-dom';
import type { BacklogSectionDto } from '../../../api/views';
import { Chip } from '../../../components/Chip';
import { routePath } from '../../../routes';
import { backlogTestIds as ids } from '../testIds';
import { SectionCapacity } from './SectionCapacity';
import { useSectionDrop } from './useSectionDrop';

// A sprint section's header: name, Current chip, item count, its load and a link to the sprint (its backlog when
// current, its capacity otherwise).
export function SectionHeader({
  section,
  projectKey,
  velocity,
  datesLabel,
  columnCount,
}: {
  section: BacklogSectionDto;
  projectKey: string;
  // What the forecast plans at, shown on a sprint with no capacity entered.
  velocity: number | null;
  // "20 Oct – 2 Nov", from the shared sprints; undefined until they are read.
  datesLabel: string | undefined;
  columnCount: number;
}) {
  const { attach, over } = useSectionDrop(section.id);
  const sprint = section.sprint;
  if (sprint === null) return null;
  const current = sprint.status === 'current';
  return (
    <tr
      ref={attach}
      data-testid={ids.planningSection}
      data-section={section.id}
      data-over={over}
      className={`h-10 border-y border-edge bg-sunken ${over ? 'outline-2 -outline-offset-2 outline-signal' : ''}`}
    >
      <td colSpan={columnCount} className="px-2">
        <div className="flex items-center gap-2">
          <b data-testid={ids.planningSectionTitle} className="text-[length:var(--text-md)]">
            {section.title}
          </b>
          {current ? <Chip tone="accent">Current</Chip> : null}
          {datesLabel ? (
            <span
              data-testid={ids.planningSectionDates}
              className="text-[length:var(--text-sm)] text-ink-muted"
            >
              {datesLabel}
            </span>
          ) : null}
          <span
            data-testid={ids.planningSectionCount}
            className="text-[length:var(--text-sm)] text-ink-muted"
          >
            {section.rows.length} items
          </span>
          <span className="flex-1" />
          <SectionCapacity
            committed={section.points}
            capacity={section.capacity}
            velocity={velocity}
          />
          <Link
            data-testid={ids.planningSectionLink}
            className="btn-ghost"
            to={routePath(current ? 'sprintBacklog' : 'capacity', {
              key: projectKey,
              sprintId: sprint.id,
            })}
          >
            {current ? 'Open sprint' : 'Capacity'}
          </Link>
        </div>
      </td>
    </tr>
  );
}
