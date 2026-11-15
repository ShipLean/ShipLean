import { useQueryClient } from '@tanstack/react-query';
import type { SprintRef } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import { Sheet } from '../../../touch/Sheet';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { useProjectSprints } from '../../workItem/workItemApi';
import { activeBacklogView } from '../estimate/cachedRow';
import { sprintSum } from '../bulk/sprintSum';
import { backlogTestIds as ids } from '../testIds';

/** A sprint to move the row into, with what it would hold after. */
interface Option {
  sprint: SprintRef;
  text: string;
  over: boolean;
}

/**
 * The row's Sprint › sheet: the Backlog and every sprint that is not past, each with its points after the row joins
 * (the same sum Planning's headers show). Picking one runs today's one-row move.
 */
export function RowSprintSheet({
  row,
  projectId,
  onPick,
  onClose,
}: {
  row: BacklogRowDto;
  projectId: string;
  onPick: (sprint: SprintRef | null) => void;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const sprints = useProjectSprints(projectId).data ?? [];
  const view = activeBacklogView(queryClient, row.id);
  const sections = view?.sections ?? [];
  const options: Option[] = sprints
    .filter((sprint) => sprint.status !== 'past')
    .map((sprint) => {
      const ref: SprintRef = { id: sprint.id, name: sprint.name, status: sprint.status };
      const held: BacklogSectionDto | undefined = sections.find((s) => s.sprint?.id === sprint.id);
      const rows = view === null ? [] : [...view.rows, ...sections.flatMap((s) => s.rows)];
      const section: BacklogSectionDto = held ?? {
        id: sprint.id,
        title: sprint.name,
        sprint: ref,
        points: rows
          .filter((other) => other.sprint?.id === sprint.id)
          .reduce((sum, other) => sum + (other.points ?? 0), 0),
        capacity: null,
        rows: [],
      };
      const sum = sprintSum(section, [row], view?.velocity ?? null);
      return { sprint: ref, text: sum.text, over: sum.over };
    });
  const choose = (sprint: SprintRef | null) => {
    onPick(sprint);
    onClose();
  };
  return (
    <Sheet open title="Move to sprint" onClose={onClose}>
      <div data-testid={ids.rowSprintSheet} className="grid gap-1">
        <button
          type="button"
          data-testid={ids.rowSprintOption}
          data-sprint=""
          aria-pressed={row.sprint === null}
          disabled={row.sprint === null}
          className={`${actionSheetRowClass} text-ink`}
          onClick={() => choose(null)}
        >
          Backlog
        </button>
        {options.map((option) => (
          <button
            key={option.sprint.id}
            type="button"
            data-testid={ids.rowSprintOption}
            data-sprint={option.sprint.id}
            aria-pressed={row.sprint?.id === option.sprint.id}
            disabled={row.sprint?.id === option.sprint.id}
            className={`${actionSheetRowClass} justify-between gap-3 text-ink`}
            onClick={() => choose(option.sprint)}
          >
            <span className="min-w-0 truncate">{option.sprint.name}</span>
            <span
              className={`flex-none text-[length:var(--text-sm)] tabular-nums ${option.over ? 'text-caution' : 'text-ink-muted'}`}
            >
              {option.text}
            </span>
          </button>
        ))}
      </div>
    </Sheet>
  );
}
