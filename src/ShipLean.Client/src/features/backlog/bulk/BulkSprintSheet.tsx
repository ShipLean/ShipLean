import type { SprintRef } from '@shiplean/core/api/refs';
import type { BacklogRowDto, BacklogSectionDto } from '@shiplean/core/api/views';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { CapacityBar } from '../../../components/CapacityBar';
import {
  ActionSheetFootnote,
  ActionSheetRowFace,
  actionSheetRowClass,
} from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { useSprintDates } from '../planning/useSprintDates';
import { backlogTestIds as ids } from '../testIds';
import { sprintSum } from './sprintSum';

/** A sprint the selection can move into, with the section whose points it adds to. */
export interface MoveTarget {
  sprint: SprintRef;
  section: BacklogSectionDto;
}

const NOTE_KEYS = 3;

function hint(...parts: (string | null | undefined)[]): string | undefined {
  const text = parts.filter((part) => part).join(' · ');
  return text === '' ? undefined : text;
}

// The footnote names a few keys, then counts the rest.
function noteKeys(rows: readonly BacklogRowDto[]): string {
  const shown = rows
    .slice(0, NOTE_KEYS)
    .map((row) => row.key)
    .join(', ');
  return rows.length > NOTE_KEYS ? `${shown} and ${rows.length - NOTE_KEYS} more` : shown;
}

const rowClass = `${actionSheetRowClass} gap-3 text-ink`;

/**
 * The bar's Sprint sheet (SHIP-716 D3): "Move n items to", the selection's keys and points, every sprint that is not
 * past with its dates, its points once the selection joins it and a capacity bar, then the Backlog. Over the limit it
 * warns and never blocks.
 */
export function BulkSprintSheet({
  targets,
  selected,
  velocity,
  projectId,
  disabled,
  onPick,
  onPickBacklog,
  onClose,
}: {
  targets: readonly MoveTarget[];
  selected: readonly BacklogRowDto[];
  velocity: VelocityDto | null;
  projectId: string;
  disabled: boolean;
  onPick: (target: MoveTarget) => void;
  onPickBacklog: () => void;
  onClose: () => void;
}) {
  const dates = useSprintDates(projectId);
  const unestimated = selected.filter((row) => row.points === null);
  const points = selected.reduce((sum, row) => sum + (row.points ?? 0), 0);
  const subtitle = `${selected.length > 3 ? `${selected.length} items` : selected.map((row) => row.key).join(', ')} · ${points} pts`;
  return (
    <Sheet
      open
      title={`Move ${selected.length} ${selected.length === 1 ? 'item' : 'items'} to`}
      subtitle={subtitle}
      onClose={onClose}
    >
      {targets.map((target) => {
        const sum = sprintSum(target.section, selected, velocity);
        const label = dates.get(target.sprint.id);
        return (
          <button
            key={target.sprint.id}
            type="button"
            data-testid={ids.bulkMoveSprint}
            data-sprint={target.sprint.id}
            disabled={disabled}
            className={rowClass}
            onClick={() => {
              onPick(target);
              onClose();
            }}
          >
            <ActionSheetRowFace
              face={{
                label: target.sprint.name,
                hint: hint(target.sprint.status === 'current' ? 'Current' : null, label),
                detail: (
                  <span className="grid justify-items-end gap-1">
                    <span
                      data-testid={ids.bulkSum}
                      data-over={sum.over}
                      className={sum.over ? 'font-semibold text-alert' : undefined}
                    >
                      {sum.text}
                    </span>
                    {sum.of === null ? null : (
                      <span className="block w-24">
                        <CapacityBar used={sum.after} capacity={sum.of} variant="capacity" />
                      </span>
                    )}
                  </span>
                ),
              }}
            />
          </button>
        );
      })}
      <button
        type="button"
        data-testid={ids.bulkMoveBacklog}
        disabled={disabled || selected.every((row) => row.sprint === null)}
        className={rowClass}
        onClick={() => {
          onPickBacklog();
          onClose();
        }}
      >
        <ActionSheetRowFace face={{ label: 'Backlog', hint: 'Out of every sprint' }} />
      </button>
      {unestimated.length === 0 ? null : (
        <ActionSheetFootnote>
          <span data-testid={ids.bulkUnestimatedNote}>
            {noteKeys(unestimated)}{' '}
            {unestimated.length === 1
              ? 'has no estimate, so it adds'
              : 'have no estimate, so they add'}{' '}
            0.
          </span>
        </ActionSheetFootnote>
      )}
    </Sheet>
  );
}
