import { useState } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import type { ParentRefDto, WorkItemDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { workItemTestIds as ids } from '../testIds';
import { useWorkItemsOfType } from '../workItemApi';
import { inputClass } from './inputClasses';

// A Feature that sits under an Epic reads `Epic › Feature`, so two Features with one name can be told apart.
const pathOf = (item: WorkItemDto): string =>
  item.type.id === 'feature' && item.parent?.type.id === 'epic'
    ? `${item.parent.title} › ${item.title}`
    : item.title;

export function ParentCandidates({
  projectId,
  itemId,
  types,
  value,
  onPick,
}: {
  projectId: string;
  itemId: string;
  types: WorkItemTypeId[];
  value: ParentRefDto | null;
  onPick: (parent: ParentRefDto | null) => void;
}) {
  const [query, setQuery] = useState('');
  // The type list is fixed per item type, so the hooks below run in the same order every render.
  const first = useWorkItemsOfType(projectId, types[0], true);
  const second = useWorkItemsOfType(projectId, types[1] ?? types[0], types.length > 1);
  const all: WorkItemDto[] = [
    ...(first.data ?? []),
    ...(types.length > 1 ? (second.data ?? []) : []),
  ];
  const needle = query.trim().toLowerCase();
  const shown = all.filter(
    (item) =>
      item.id !== itemId &&
      (needle === '' ||
        pathOf(item).toLowerCase().includes(needle) ||
        item.key.toLowerCase().includes(needle)),
  );
  return (
    <>
      <input
        autoFocus
        data-testid={ids.parentSearch}
        aria-label="Search parents"
        placeholder="Search by title or key"
        className={`${inputClass} m-1`}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <button
        type="button"
        data-testid={ids.parentOption}
        role="menuitemradio"
        aria-checked={value === null}
        className="h-[30px] rounded-control px-2 text-left text-[length:var(--text-sm)] text-ink-muted hover:bg-signal-soft"
        onClick={() => onPick(null)}
      >
        No parent
      </button>
      {shown.map((item) => (
        <button
          key={item.id}
          type="button"
          data-testid={ids.parentOption}
          role="menuitemradio"
          aria-checked={value?.id === item.id}
          className="flex h-[30px] items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={() => onPick({ id: item.id, key: item.key, type: item.type, title: item.title })}
        >
          <TypeIcon type={item.type.id} />
          <span className="truncate">{pathOf(item)}</span>
          <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">{item.key}</span>
        </button>
      ))}
    </>
  );
}
