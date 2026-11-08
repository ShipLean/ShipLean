import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { Chip } from '../../components/Chip';
import { routePath } from '../../routes';
import { Sheet } from '../../touch/Sheet';
import { sprintHeaderTestIds as ids } from './testIds';

const GROUPS = [
  { status: 'past', label: 'Past' },
  { status: 'current', label: 'Current' },
  { status: 'future', label: 'Future' },
] as const satisfies readonly { status: SprintDto['status']; label: string }[];

const ROW =
  'grid min-h-[var(--touch-target)] w-full grid-cols-[20px_minmax(0,1fr)_auto] items-center gap-x-2 rounded-control px-2 py-1.5 text-left';

/**
 * The sprint picker at 900px and below (SHIP-724 D6): a sheet of every sprint, Past then Current then Future, each with
 * its dates and status; the sprint on screen is ticked; All sprints sits at the foot. Picking keeps the tab.
 */
export function SprintPickerSheet({
  open,
  project,
  sprint,
  sprints,
  tabLabel,
  onPick,
  onClose,
}: {
  open: boolean;
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
  tabLabel: string;
  onPick: (picked: SprintDto) => void;
  onClose: () => void;
}) {
  const footer = (
    <Link
      data-testid={ids.pickerSheetAllSprints}
      to={routePath('sprints', { key: project.key })}
      className="flex min-h-[var(--touch-target)] flex-1 items-center justify-between rounded-control px-2 text-[length:var(--text-base)] text-ink no-underline"
      onClick={onClose}
    >
      All sprints <span aria-hidden="true">›</span>
    </Link>
  );
  return (
    <Sheet open={open} title="Sprints" onClose={onClose} footer={footer}>
      <p
        data-testid={ids.pickerSheetNote}
        className="m-0 px-2 pb-1 text-[length:var(--text-sm)] text-ink-muted"
      >
        {`Opens the same tab (${tabLabel}) of the sprint you pick.`}
      </p>
      {GROUPS.flatMap((group) =>
        sprints
          .filter((s) => s.status === group.status)
          .map((s) => {
            const picked = s.id === sprint.id;
            return (
              <button
                key={s.id}
                type="button"
                data-testid={ids.pickerSheetRow}
                data-status={s.status}
                aria-current={picked ? 'true' : undefined}
                className={`${ROW} ${picked ? 'bg-signal-soft' : 'active:bg-signal-soft'}`}
                onClick={() => onPick(s)}
              >
                <span
                  aria-hidden="true"
                  data-testid={picked ? ids.pickerSheetTick : undefined}
                  className="text-signal"
                >
                  {picked ? '✓' : ''}
                </span>
                <span className="grid min-w-0">
                  <span className="truncate text-[length:var(--text-base)] text-ink">{s.name}</span>
                  <span className="text-[length:var(--text-sm)] text-ink-muted">
                    {s.datesLabel}
                  </span>
                </span>
                <Chip tone={s.status === 'current' ? 'accent' : 'muted'}>{group.label}</Chip>
              </button>
            );
          }),
      )}
    </Sheet>
  );
}
