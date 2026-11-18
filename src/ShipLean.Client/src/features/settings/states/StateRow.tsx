import { useEffect, useEffectEvent, useRef, useState } from 'react';
import type { StateCategory } from '@shiplean/core/api/refs';
import { StateDot } from '../../../components/StateDot';
import { MoreButton } from '../../../touch/MoreButton';
import { useLongPressDrag } from '../../../touch/useLongPressDrag';
import { useTouchShell } from '../../../touch/useTouchShell';
import { settingsTestIds } from '../testIds';
import { CATEGORIES, CATEGORY_LABELS, parseCategory } from './categories';
import { itemsIn, type StateRowModel } from './stateModel';

const field =
  'h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60 touch:h-[var(--touch-target)] touch:min-w-0';
const small =
  'h-6 rounded-control border border-edge px-1.5 text-[length:var(--text-sm)] text-ink-muted disabled:opacity-40 touch:h-[var(--touch-target)] touch:min-w-[var(--touch-target)] touch:px-3';

export function StateRow({
  row,
  position,
  count,
  readOnly,
  error,
  start,
  column,
  blocked,
  onName,
  onCategory,
  dropLine,
  onMove,
  onDelete,
  onTouchOver,
  onTouchDrop,
  onTouchDragEnd,
}: {
  row: StateRowModel;
  position: number;
  count: number;
  readOnly: boolean;
  error: string | undefined;
  /** The first Proposed state: new items start here. */
  start: boolean;
  /** The board column the state sits in; undefined = Not mapped, '' = still loading. */
  column: string | undefined;
  /** Why Delete is off, shown as its tooltip and beside it. */
  blocked: string | undefined;
  onName(name: string): void;
  onCategory(category: StateCategory): void;
  /** At touch, while another row is dragged over this one: where the drop line goes. */
  dropLine?: 'above' | 'below';
  onMove(by: -1 | 1): void;
  onDelete(): void;
  /** At touch: the dragged row is over the row with this id (null: off every row). */
  onTouchOver(targetId: string | null): void;
  /** At touch: the ⠿ handle was held, dragged and let go over the row with this id (null: off every row). */
  onTouchDrop(targetId: string | null): void;
  /** At touch: this row's drag ended, let go or cancelled. */
  onTouchDragEnd(): void;
}) {
  const items = itemsIn(row);
  const { touch } = useTouchShell();
  const drag = useLongPressDrag({
    handle: true,
    onMove: (target) => onTouchOver(target?.dataset.dropTarget ?? null),
    onDrop: (target) => onTouchDrop(target?.dataset.dropTarget ?? null),
  });
  // A touchcancel ends the drag with no drop, so the end is the lifted state going away, not onDrop.
  const wasLifted = useRef(false);
  const dragEnded = useEffectEvent(onTouchDragEnd);
  useEffect(() => {
    if (wasLifted.current && !drag.lifted) dragEnded();
    wasLifted.current = drag.lifted;
  }, [drag.lifted]);
  // Rename focuses the name after the sheet has closed: the sheet's close hands focus back to the ⋯ first.
  const name = useRef<HTMLInputElement>(null);
  const [renameAsked, setRenameAsked] = useState(0);
  useEffect(() => {
    if (renameAsked > 0) name.current?.focus();
  }, [renameAsked]);
  const editable = touch && !readOnly;
  // SHIP-1809 P5: at touch the row's actions are one ⋯ sheet; Delete… sits after a divider.
  const actions = [
    {
      id: 'rename',
      label: 'Rename',
      onSelect: () => setRenameAsked((n) => n + 1),
    },
    { id: 'up', label: 'Move up', disabled: position === 0, onSelect: () => onMove(-1) },
    {
      id: 'down',
      label: 'Move down',
      disabled: position === count - 1,
      onSelect: () => onMove(1),
    },
    {
      id: 'delete',
      label: 'Delete…',
      tone: 'danger' as const,
      group: '',
      disabled: blocked !== undefined,
      hint: blocked,
      onSelect: onDelete,
    },
  ];
  const line =
    dropLine === 'above'
      ? 'touch:shadow-[inset_0_2px_0_var(--color-signal)]'
      : dropLine === 'below'
        ? 'touch:shadow-[inset_0_-2px_0_var(--color-signal)]'
        : '';
  return (
    <tr
      data-testid={settingsTestIds.stateRow}
      data-state-id={row.id}
      data-drop-target={editable ? row.id : undefined}
      data-lifted={drag.lifted || undefined}
      className={`border-b border-edge last:border-b-0 touch:flex touch:flex-wrap touch:items-center touch:gap-x-2 touch:gap-y-2 touch:px-3 touch:py-3 ${line} ${drag.lifted ? 'touch:rounded-panel touch:outline-2 touch:-outline-offset-2 touch:outline-signal' : ''}`}
    >
      <td className="w-16 px-3 py-2 whitespace-nowrap touch:hidden">
        {readOnly ? (
          <span className="text-ink-muted">{position + 1}</span>
        ) : (
          <span className="flex items-center gap-1">
            <button
              type="button"
              data-testid={settingsTestIds.stateMoveUp}
              aria-label={`Move ${row.name} up`}
              disabled={position === 0}
              className={small}
              onClick={() => onMove(-1)}
            >
              ↑
            </button>
            <button
              type="button"
              data-testid={settingsTestIds.stateMoveDown}
              aria-label={`Move ${row.name} down`}
              disabled={position === count - 1}
              className={small}
              onClick={() => onMove(1)}
            >
              ↓
            </button>
          </span>
        )}
      </td>
      <td className="px-3 py-2 touch:order-1 touch:w-full touch:min-w-0 touch:p-0">
        <span className="flex items-center gap-2">
          {editable ? (
            <button
              type="button"
              data-testid={settingsTestIds.stateHandle}
              aria-label={`Move ${row.name}: hold and drag`}
              className={`inline-grid size-[var(--touch-target)] flex-none place-items-center rounded-control text-ink-muted ${drag.lifted ? 'bg-signal-soft text-signal' : ''}`}
              {...drag.props}
            >
              ⠿
            </button>
          ) : null}
          <StateDot category={row.category} />
          <input
            ref={name}
            data-testid={settingsTestIds.stateName}
            id={`settings-field-state-${row.id}`}
            aria-label="State name"
            // w-0: an input's min-content is its intrinsic width (~20 characters), which pushed the row past 390px.
            className={`${field} touch:w-0 touch:flex-1`}
            value={row.name}
            maxLength={30}
            disabled={readOnly}
            autoComplete="off"
            onChange={(e) => onName(e.target.value)}
            onKeyDown={(e) => {
              if (e.altKey && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
                e.preventDefault();
                onMove(e.key === 'ArrowUp' ? -1 : 1);
              }
            }}
          />
          {start ? (
            <span
              data-testid={settingsTestIds.stateStart}
              title="New items start here"
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              start
            </span>
          ) : null}
          {row.fresh ? <span className="text-[length:var(--text-xs)] text-signal">new</span> : null}
          {row.savedName !== undefined && row.savedName !== row.name ? (
            <span className="text-[length:var(--text-xs)] text-ink-muted touch:hidden">
              was {row.savedName}
            </span>
          ) : null}
          {editable ? <MoreButton label={row.name} actions={actions} /> : null}
        </span>
        {error === undefined ? null : (
          <div
            role="alert"
            data-testid={settingsTestIds.stateRowError}
            className="mt-1 text-[length:var(--text-sm)] text-alert"
          >
            {error}
          </div>
        )}
      </td>
      <td className={`px-3 py-2 touch:order-2 touch:p-0 ${editable ? 'touch:ml-10' : ''}`}>
        <select
          data-testid={settingsTestIds.stateCategory}
          aria-label={`Category of ${row.name}`}
          className={field}
          value={row.category}
          disabled={readOnly}
          onChange={(e) => onCategory(parseCategory(e.target.value))}
        >
          {CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {CATEGORY_LABELS[c]}
            </option>
          ))}
        </select>
      </td>
      <td
        data-testid={settingsTestIds.stateItems}
        className="px-3 py-2 text-right text-ink-muted touch:order-3 touch:ml-auto touch:p-0 touch:text-[length:var(--text-sm)] touch:before:content-['Items_']"
        title={
          row.incoming > 0
            ? `${row.itemCount} now, ${row.incoming} more moving here on save`
            : undefined
        }
      >
        {items}
        {row.incoming > 0 ? (
          <span className="text-[length:var(--text-xs)]"> +{row.incoming}</span>
        ) : null}
      </td>
      <td
        className={`px-3 py-2 text-ink-muted touch:order-8 touch:w-full touch:p-0 touch:text-[length:var(--text-sm)] ${editable ? 'touch:pl-10' : ''}`}
      >
        <span data-testid={settingsTestIds.stateColumnLabel} className="hidden touch:inline">
          Board column ·{' '}
        </span>
        <span data-testid={settingsTestIds.stateColumn}>{column ?? 'Not mapped'}</span>
      </td>
      <td className="px-3 py-2 text-right touch:contents">
        {readOnly ? null : (
          <span className="flex items-center justify-end gap-2 touch:contents">
            {blocked === undefined ? null : (
              <span
                data-testid={settingsTestIds.stateDeleteBlock}
                className="text-[length:var(--text-xs)] text-ink-muted touch:hidden"
              >
                {blocked}
              </span>
            )}
            <button
              type="button"
              data-testid={settingsTestIds.stateDelete}
              aria-label={`Delete ${row.name}`}
              disabled={blocked !== undefined}
              title={blocked}
              className={`${small} touch:hidden`}
              onClick={onDelete}
            >
              Delete
            </button>
          </span>
        )}
      </td>
    </tr>
  );
}
