import { useRef, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { routePath } from '../../../routes';
import type { SheetAction } from '../../../touch/ActionSheet';
import { MoreButton } from '../../../touch/MoreButton';
import { canCloseSprint } from '../end/canCloseSprint';
import { sprintsTestIds as ids } from '../testIds';
import type { SprintRowProps } from './sprintRowProps';
import { SprintSheetIcon } from './SprintSheetIcon';

const STATUS_LABEL: Record<SprintDto['status'], string> = {
  past: 'Past',
  current: 'Current',
  future: 'Future',
};

const PAST_FOOTNOTE =
  'Ended. Its committed and completed points come from the history at its end date.';

/**
 * A sprint at 900px and below (SHIP-1807 P4): name and dates over the goal (two lines at most), then Capacity, Committed
 * and Completed with their labels, then any capacity notes (P8). The whole row opens the sprint's Backlog; ⋯ opens its
 * sheet (P6).
 */
export function SprintTouchRow({
  projectKey,
  sprint,
  figures,
  velocity,
  onEdit,
  onCloseSprint,
}: SprintRowProps) {
  const navigate = useNavigate();
  // The ⋯ button lives in the shared MoreButton, so focus goes back to it through its wrapper when Edit sprint closes.
  const more = useRef<HTMLSpanElement>(null);
  const metrics = figures?.metrics;
  const capacity = figures?.capacity;
  const committed = metrics === undefined || metrics.committed === 0 ? null : metrics.committed;
  const completed = metrics === undefined || sprint.status === 'future' ? null : metrics.completed;
  const notes = (capacity?.members ?? [])
    .filter((member) => member.note !== null && member.note !== '')
    .map((member) => `${member.user.name}: ${member.note}`);
  const params = { key: projectKey, sprintId: sprint.id };
  const backlog = routePath('sprintBacklog', params);
  const open = (
    id: string,
    label: string,
    icon: Parameters<typeof SprintSheetIcon>[0]['name'],
    path: string,
    detail?: string,
  ): SheetAction => ({
    id,
    label,
    group: 'Open',
    icon: <SprintSheetIcon name={icon} />,
    detail,
    trailing: 'chevron',
    onSelect: () => void navigate(path),
  });
  const actions: SheetAction[] = [
    open(
      'backlog',
      'Backlog',
      'backlog',
      backlog,
      committed === null ? undefined : `${committed} pts`,
    ),
    open('taskboard', 'Taskboard', 'taskboard', routePath('taskboard', params)),
    open(
      'capacity',
      'Capacity',
      'capacity',
      routePath('capacity', params),
      capacity !== undefined && capacity.members.length > 0
        ? `${capacity.teamTotal} pts`
        : undefined,
    ),
    ...(sprint.status === 'future'
      ? []
      : [
          open('retro', 'Retro', 'retro', routePath('sprintRetro', params)),
          open('demo', 'Demo', 'demo', routePath('sprintDemo', params)),
        ]),
    {
      id: 'edit-sprint',
      label: 'Edit sprint',
      hint: 'Name, dates and goal',
      icon: <SprintSheetIcon name="edit" />,
      onSelect: () => onEdit(more.current?.querySelector('button') ?? null),
    },
    ...(sprint.status === 'current' && canCloseSprint(sprint)
      ? [
          {
            id: 'close-sprint',
            label: 'Close sprint',
            hint: 'Choose where unfinished work goes',
            icon: <SprintSheetIcon name="close" />,
            trailing: 'chevron' as const,
            onSelect: onCloseSprint,
          },
        ]
      : []),
  ];
  return (
    <li
      data-testid={ids.listRow}
      data-sprint={sprint.name}
      data-status={sprint.status}
      className={`relative grid min-w-0 gap-1 border-b border-edge py-2 pr-1 pl-3 last:border-b-0 ${sprint.status === 'current' ? 'bg-signal-soft/50' : ''}`}
    >
      <div className="flex min-w-0 items-center gap-2">
        {/* The link's ::after covers the row, so a tap anywhere but ⋯ opens the Backlog. */}
        <Link
          data-testid={ids.rowLink}
          className="inline-flex min-h-[var(--touch-target)] min-w-0 flex-none items-center truncate text-[length:var(--text-md)] font-semibold text-inherit no-underline after:absolute after:inset-0 after:content-['']"
          to={backlog}
        >
          {sprint.name}
        </Link>
        <span className="min-w-0 flex-1 truncate text-[length:var(--text-sm)] text-ink-muted">
          {sprint.datesLabel}
        </span>
        <span ref={more} className="relative z-10 flex-none">
          <MoreButton
            label={sprint.name}
            subtitle={`${sprint.datesLabel} · ${STATUS_LABEL[sprint.status]}`}
            footer={sprint.status === 'past' ? PAST_FOOTNOTE : undefined}
            actions={actions}
          />
        </span>
      </div>
      <p data-testid={ids.rowGoal} className="m-0 line-clamp-2 pr-2">
        {sprint.goal === '' ? <i className="text-ink-muted">No goal yet</i> : sprint.goal}
      </p>
      <div className="flex flex-wrap gap-x-4 pr-2 text-[length:var(--text-sm)] text-ink-muted tabular-nums">
        <Figure label="Capacity" testId={ids.rowCapacity}>
          {capacity === undefined ? (
            '—'
          ) : capacity.members.length > 0 ? (
            <b className="text-ink">{capacity.teamTotal}</b>
          ) : velocity === undefined ? (
            'velocity'
          ) : (
            `${velocity} · velocity`
          )}
        </Figure>
        <Figure label="Committed" testId={ids.rowCommitted}>
          {committed === null ? '—' : <b className="text-ink">{committed}</b>}
        </Figure>
        <Figure label="Completed" testId={ids.rowCompleted}>
          {completed === null ? (
            '—'
          ) : (
            <>
              <b className="text-ink">{completed}</b>
              {sprint.status === 'current' ? ' so far' : null}
            </>
          )}
        </Figure>
      </div>
      {notes.length === 0 ? null : (
        <p
          data-testid={ids.rowNotes}
          className="m-0 pr-2 text-[length:var(--text-sm)] whitespace-pre-line text-ink-muted before:mr-1.5 before:content-['✎']"
        >
          {notes.join('\n')}
        </p>
      )}
    </li>
  );
}

/** One labelled figure; the value carries the test id. */
function Figure({
  label,
  testId,
  children,
}: {
  label: string;
  testId: string;
  children: ReactNode;
}) {
  return (
    <span className="whitespace-nowrap">
      {label} <span data-testid={testId}>{children}</span>
    </span>
  );
}
