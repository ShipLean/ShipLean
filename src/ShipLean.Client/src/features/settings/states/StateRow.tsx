import type { StateCategory } from '@shiplean/core/api/refs';
import { StateDot } from '../../../components/StateDot';
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
  onMove,
  onDelete,
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
  onMove(by: -1 | 1): void;
  onDelete(): void;
}) {
  const items = itemsIn(row);
  return (
    <tr
      data-testid={settingsTestIds.stateRow}
      data-state-id={row.id}
      className="border-b border-edge last:border-b-0 touch:flex touch:flex-wrap touch:items-center touch:gap-x-2 touch:gap-y-2 touch:px-3 touch:py-3"
    >
      <td className="w-16 px-3 py-2 whitespace-nowrap touch:order-5 touch:w-auto touch:p-0">
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
      <td className="px-3 py-2 touch:order-1 touch:w-full touch:p-0">
        <span className="flex items-center gap-2 touch:flex-wrap">
          <StateDot category={row.category} />
          <input
            data-testid={settingsTestIds.stateName}
            id={`settings-field-state-${row.id}`}
            aria-label="State name"
            className={`${field} touch:min-w-0 touch:flex-1`}
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
            <span className="text-[length:var(--text-xs)] text-ink-muted">was {row.savedName}</span>
          ) : null}
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
      <td className="px-3 py-2 touch:order-2 touch:p-0">
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
        className="px-3 py-2 text-right text-ink-muted touch:order-3 touch:p-0"
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
        data-testid={settingsTestIds.stateColumn}
        className="px-3 py-2 text-ink-muted touch:order-8 touch:w-full touch:p-0 touch:text-[length:var(--text-sm)] touch:before:content-['Board:_']"
      >
        {column ?? 'Not mapped'}
      </td>
      <td className="px-3 py-2 text-right touch:contents">
        {readOnly ? null : (
          <span className="flex items-center justify-end gap-2 touch:contents">
            {blocked === undefined ? null : (
              <span
                data-testid={settingsTestIds.stateDeleteBlock}
                className="text-[length:var(--text-xs)] text-ink-muted touch:order-7 touch:w-full"
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
              className={`${small} touch:order-6`}
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
