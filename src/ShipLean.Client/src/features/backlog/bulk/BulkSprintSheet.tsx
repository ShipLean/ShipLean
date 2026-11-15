import type { SprintRef } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';
import { sprintSum } from './sprintSum';

/** A sprint the selection can move into, with the section whose points it adds to. */
export interface MoveTarget {
  sprint: SprintRef;
  section: BacklogSectionDto;
}

/** The bar's Sprint sheet: every sprint that is not past, each with its points once the selection joins it. Over the limit it warns and never blocks. */
export function BulkSprintSheet({
  targets,
  selected,
  velocity,
  disabled,
  onPick,
  onClose,
}: {
  targets: readonly MoveTarget[];
  selected: readonly BacklogRowDto[];
  velocity: VelocityDto | null;
  disabled: boolean;
  onPick: (target: MoveTarget) => void;
  onClose: () => void;
}) {
  return (
    <Sheet open title="Move to sprint" onClose={onClose}>
      {targets.map((target) => {
        const sum = sprintSum(target.section, selected, velocity);
        return (
          <button
            key={target.sprint.id}
            type="button"
            data-testid={ids.bulkMoveSprint}
            data-sprint={target.sprint.id}
            disabled={disabled}
            className={`${actionSheetRowClass} justify-between gap-3 text-ink`}
            onClick={() => {
              onPick(target);
              onClose();
            }}
          >
            <span className="min-w-0 truncate">
              {target.sprint.name}
              {target.sprint.status === 'current' ? (
                <span className="ml-1.5 font-normal text-ink-muted">current</span>
              ) : null}
            </span>
            <span
              data-testid={ids.bulkSum}
              data-over={sum.over}
              className={`flex-none text-[length:var(--text-sm)] tabular-nums ${sum.over ? 'font-semibold text-alert' : 'text-ink-muted'}`}
            >
              {sum.text}
            </span>
          </button>
        );
      })}
    </Sheet>
  );
}
