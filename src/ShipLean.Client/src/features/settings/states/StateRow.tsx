import type { StateCategory } from '../../../api/refs';
import { StateDot } from '../../../components/StateDot';
import { settingsTestIds } from '../testIds';
import { CATEGORIES, CATEGORY_LABELS, parseCategory } from './categories';
import { itemsIn, type StateRowModel } from './stateModel';

const field =
  'h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';
const small =
  'h-6 rounded-control border border-edge px-1.5 text-[length:var(--text-sm)] text-ink-muted disabled:opacity-40';

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
  onStartsAgent,
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
  onStartsAgent(startsAgent: boolean): void;
  onMove(by: -1 | 1): void;
  onDelete(): void;
}) {
  const items = itemsIn(row);
  return (
    <tr
      data-testid={settingsTestIds.stateRow}
      data-state-id={row.id}
      className="border-b border-edge last:border-b-0"
    >
      <td className="w-16 px-3 py-2 whitespace-nowrap">
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
      <td className="px-3 py-2">
        <span className="flex items-center gap-2">
          <StateDot category={row.category} />
          <input
            data-testid={settingsTestIds.stateName}
            id={`settings-field-state-${row.id}`}
            aria-label="State name"
            className={field}
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
      <td className="px-3 py-2">
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
      <td className="px-3 py-2 text-center">
        <input
          type="checkbox"
          data-testid={settingsTestIds.stateStartsAgent}
          aria-label={`Starts an agent: ${row.name}`}
          checked={row.startsAgent}
          disabled={readOnly}
          onChange={(e) => onStartsAgent(e.target.checked)}
        />
      </td>
      <td
        data-testid={settingsTestIds.stateItems}
        className="px-3 py-2 text-right text-ink-muted"
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
      <td data-testid={settingsTestIds.stateColumn} className="px-3 py-2 text-ink-muted">
        {column ?? 'Not mapped'}
      </td>
      <td className="px-3 py-2 text-right">
        {readOnly ? null : (
          <span className="flex items-center justify-end gap-2">
            {blocked === undefined ? null : (
              <span
                data-testid={settingsTestIds.stateDeleteBlock}
                className="text-[length:var(--text-xs)] text-ink-muted"
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
              className={small}
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
