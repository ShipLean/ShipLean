import { Link } from 'react-router-dom';
import type { SprintCapacityDto, SprintDto } from '../../../api/sprints';
import { Chip } from '../../../components/Chip';
import { routePath } from '../../../routes';
import type { SprintFigures } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

interface SprintRowProps {
  projectKey: string;
  sprint: SprintDto;
  figures: SprintFigures | undefined;
  /** What a sprint with no entered capacity plans against; undefined while it loads. */
  velocity: number | undefined;
  /** Opens the Edit sprint rail for this sprint. */
  onEdit: (trigger: HTMLButtonElement) => void;
}

const STATUS_CHIP = {
  past: { tone: 'muted', label: 'Past' },
  current: { tone: 'accent', label: 'Current' },
  future: { tone: 'muted', label: 'Future' },
} as const;

const NUMBER_CELL = 'px-3 py-2 text-right font-semibold tabular-nums';

/** One sprint: its link to the backlog, dates, status, goal, team capacity (or velocity when none is entered), committed and completed points. */
export function SprintRow({ projectKey, sprint, figures, velocity, onEdit }: SprintRowProps) {
  const chip = STATUS_CHIP[sprint.status];
  const metrics = figures?.metrics;
  const capacity = figures?.capacity;
  const committed =
    metrics === undefined || metrics.committed === 0 ? '—' : String(metrics.committed);
  const completed =
    metrics === undefined || sprint.status === 'future'
      ? '—'
      : sprint.status === 'current'
        ? `${metrics.completed} so far`
        : String(metrics.completed);
  return (
    <tr
      data-testid={ids.listRow}
      data-sprint={sprint.name}
      data-status={sprint.status}
      className={`border-b border-edge last:border-b-0 ${sprint.status === 'current' ? 'bg-signal-soft/50' : ''}`}
    >
      <td className="px-3 py-2 whitespace-nowrap">
        <Link
          data-testid={ids.rowLink}
          className="font-semibold text-inherit no-underline hover:underline"
          to={routePath('sprintBacklog', { key: projectKey, sprintId: sprint.id })}
        >
          {sprint.name}
        </Link>
      </td>
      <td className="px-3 py-2 text-[length:var(--text-sm)] whitespace-nowrap">
        {sprint.datesLabel}
      </td>
      <td data-testid={ids.rowStatus} className="px-3 py-2 whitespace-nowrap">
        <Chip tone={chip.tone}>{chip.label}</Chip>
      </td>
      <td
        data-testid={ids.rowGoal}
        className="px-3 py-2 text-[length:var(--text-sm)] text-ink-muted"
      >
        {sprint.goal === '' ? <i>No goal yet</i> : sprint.goal}
      </td>
      <td data-testid={ids.rowCapacity} className={NUMBER_CELL}>
        {capacity === undefined ? (
          '—'
        ) : capacity.members.length > 0 ? (
          <CapacityTotal capacity={capacity} />
        ) : (
          <span className="font-normal text-ink-muted">
            {velocity === undefined ? 'velocity' : `${velocity} · velocity`}
          </span>
        )}
      </td>
      <td data-testid={ids.rowCommitted} className={NUMBER_CELL}>
        {committed}
      </td>
      <td data-testid={ids.rowCompleted} className={NUMBER_CELL}>
        {completed}
      </td>
      <td className="px-3 py-2 text-right">
        <button
          type="button"
          data-testid={ids.rowEdit}
          aria-label={`Edit ${sprint.name}`}
          className="inline-flex h-7 items-center rounded-control border border-transparent bg-transparent px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal"
          onClick={(event) => onEdit(event.currentTarget)}
        >
          Edit
        </button>
      </td>
    </tr>
  );
}

/** The team total; when members left notes it is dotted-underlined and the notes show on hover. */
function CapacityTotal({ capacity }: { capacity: SprintCapacityDto }) {
  const notes = capacity.members
    .filter((member) => member.note !== null && member.note !== '')
    .map((member) => `${member.user.name}: ${member.note}`);
  if (notes.length === 0) return <>{capacity.teamTotal}</>;
  return (
    <span
      data-testid={ids.rowCapacityNote}
      title={notes.join('\n')}
      className="cursor-help underline decoration-dotted"
    >
      {capacity.teamTotal}
    </span>
  );
}
