import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '../../../api/refs';
import { isStoryLevel, typeName } from '../../../api/workItemTypes';
import type { DuplicateItem } from '../../../api/backlogWrites';
import { backlogTestIds as ids } from '../testIds';
import { pointsError, type PasteRow as Row } from './pasteParse';

const itemTypes = WORK_ITEM_TYPE_IDS.filter(isStoryLevel);

// One row of the review grid: a type, a title, points and a way to leave the line out.
// A task keeps its story's place below it, and its type is fixed.
export function PasteRow({
  row,
  duplicate,
  onChange,
  onTitleCommit,
  onRemove,
}: {
  row: Row;
  duplicate: DuplicateItem | null;
  onChange: (patch: Partial<Pick<Row, 'type' | 'title' | 'points'>>) => void;
  /** The title input lost focus; the host checks it again if it changed. */
  onTitleCommit: () => void;
  onRemove: () => void;
}) {
  const badPoints = pointsError(row.points);
  const emptyTitle = row.title.trim() === '';
  return (
    <tr data-testid={ids.pasteRow} data-kind={row.kind} className="border-b border-edge">
      <td className="w-36 py-1 pr-2">
        {row.kind === 'task' ? (
          <span className="pl-4 text-ink-muted">{typeName('task')}</span>
        ) : (
          <select
            data-testid={ids.pasteRowType}
            aria-label="Type"
            className="field w-full"
            value={row.type}
            onChange={(event) => onChange({ type: event.target.value as WorkItemTypeId })}
          >
            {itemTypes.map((id) => (
              <option key={id} value={id}>
                {typeName(id)}
              </option>
            ))}
          </select>
        )}
      </td>
      <td className={`py-1 pr-2 ${row.kind === 'task' ? 'pl-4' : ''}`}>
        <input
          data-testid={ids.pasteRowTitle}
          aria-label="Title"
          aria-invalid={emptyTitle}
          className="field w-full"
          value={row.title}
          onChange={(event) => onChange({ title: event.target.value })}
          onBlur={onTitleCommit}
        />
        {row.promoted ? (
          <span className="text-[length:var(--text-xs)] text-caution">
            Indented with nothing above it, so it is an item.
          </span>
        ) : null}
        {duplicate ? (
          <span
            data-testid={ids.pasteRowDuplicate}
            className="block text-[length:var(--text-xs)] text-caution"
          >
            Looks like {duplicate.key}: {duplicate.title}
          </span>
        ) : null}
      </td>
      <td className="w-20 py-1 pr-2">
        <input
          data-testid={ids.pasteRowPoints}
          aria-label="Points"
          aria-invalid={badPoints !== null}
          title={badPoints ?? undefined}
          inputMode="decimal"
          placeholder="?"
          className={`field w-full ${badPoints ? 'border-alert' : ''}`}
          value={row.points}
          onChange={(event) => onChange({ points: event.target.value })}
        />
      </td>
      <td className="w-8 py-1">
        <button
          type="button"
          aria-label="Leave out"
          className="text-ink-muted hover:text-ink"
          onClick={onRemove}
        >
          ×
        </button>
      </td>
    </tr>
  );
}
