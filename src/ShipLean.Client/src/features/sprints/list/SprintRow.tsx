import { useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { SprintCapacityDto, SprintDto } from '@shiplean/core/api/sprints';
import { Chip } from '../../../components/Chip';
import { routePath } from '../../../routes';
import { MoreButton } from '../../../touch/MoreButton';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { SprintFigures } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

interface SprintRowProps {
  projectKey: string;
  sprint: SprintDto;
  figures: SprintFigures | undefined;
  /** What a sprint with no entered capacity plans against; undefined while it loads. */
  velocity: number | undefined;
  /** Opens the Edit sprint rail for this sprint; `trigger` is the button to return focus to, none from the ⋯ sheet. */
  onEdit: (trigger: HTMLButtonElement | null) => void;
}

const STATUS_CHIP = {
  past: { tone: 'muted', label: 'Past' },
  current: { tone: 'accent', label: 'Current' },
  future: { tone: 'muted', label: 'Future' },
} as const;

const NUMBER_CELL = 'px-3 py-2 text-right font-semibold tabular-nums';

/** One sprint: its link to the backlog, dates, status, goal, team capacity (or velocity when none is entered), committed and completed points. */
export function SprintRow(props: SprintRowProps) {
  const { touch } = useTouchShell();
  return touch ? <SprintTouchRow {...props} /> : <SprintTableRow {...props} />;
}

function SprintTableRow({ projectKey, sprint, figures, velocity, onEdit }: SprintRowProps) {
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

/** The team capacity as a plain line: the total, or the planning velocity when nobody entered any. */
function capacityText(
  capacity: SprintCapacityDto | undefined,
  velocity: number | undefined,
): string {
  if (capacity === undefined) return '—';
  if (capacity.members.length > 0) return `${capacity.teamTotal} capacity`;
  return velocity === undefined ? 'velocity' : `${velocity} · velocity`;
}

/** At 900px and below (SHIP-723, the SHIP-724 D2 row): name and status over the points, dates and capacity, the goal and any notes under. */
function SprintTouchRow({ projectKey, sprint, figures, velocity, onEdit }: SprintRowProps) {
  const navigate = useNavigate();
  // The ⋯ button lives in the shared MoreButton, so focus goes back to it through its wrapper when Edit sprint closes.
  const more = useRef<HTMLSpanElement>(null);
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
  const notes = (capacity?.members ?? [])
    .filter((member) => member.note !== null && member.note !== '')
    .map((member) => `${member.user.name}: ${member.note}`);
  const backlog = routePath('sprintBacklog', { key: projectKey, sprintId: sprint.id });
  return (
    <li
      data-testid={ids.listRow}
      data-sprint={sprint.name}
      data-status={sprint.status}
      className={`grid min-w-0 gap-0.5 border-b border-edge py-1 pr-1 pl-3 ${sprint.status === 'current' ? 'bg-signal-soft/50' : ''}`}
    >
      <div className="flex min-w-0 items-center gap-2">
        <Link
          data-testid={ids.rowLink}
          className="inline-flex min-h-[var(--touch-target)] min-w-0 items-center truncate font-semibold text-inherit no-underline"
          to={backlog}
        >
          {sprint.name}
        </Link>
        <span data-testid={ids.rowStatus} className="flex-none">
          <Chip tone={chip.tone}>{chip.label}</Chip>
        </span>
        <span className="flex-1" />
        <span className="flex-none text-[length:var(--text-sm)] tabular-nums">
          <b data-testid={ids.rowCommitted}>{committed}</b>
          <span className="text-ink-muted"> committed · </span>
          <b data-testid={ids.rowCompleted}>{completed}</b>
          <span className="text-ink-muted"> done</span>
        </span>
        <span ref={more} className="flex-none">
          <MoreButton
            label={sprint.name}
            actions={[
              { id: 'open', label: 'Open', onSelect: () => void navigate(backlog) },
              {
                id: 'edit-sprint',
                label: 'Edit sprint',
                onSelect: () => onEdit(more.current?.querySelector('button') ?? null),
              },
            ]}
          />
        </span>
      </div>
      <div className="pr-2 text-[length:var(--text-sm)] text-ink-muted">
        {sprint.datesLabel} ·{' '}
        <span data-testid={ids.rowCapacity}>{capacityText(capacity, velocity)}</span>
      </div>
      <p
        data-testid={ids.rowGoal}
        className="m-0 line-clamp-2 pr-2 text-[length:var(--text-sm)] text-ink-muted"
      >
        {sprint.goal === '' ? <i>No goal yet</i> : sprint.goal}
      </p>
      {notes.length === 0 ? null : (
        <p
          data-testid={ids.rowNotes}
          className="m-0 pr-2 text-[length:var(--text-xs)] whitespace-pre-line text-ink-muted"
        >
          {notes.join('\n')}
        </p>
      )}
    </li>
  );
}
