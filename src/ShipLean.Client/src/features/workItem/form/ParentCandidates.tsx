import { Fragment, useState } from 'react';
import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { ParentRefDto, WorkItemDto } from '@shiplean/core/api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import {
  actionSheetCheckedClass,
  actionSheetRowClass,
  ActionSheetFootnote,
  ActionSheetGroupHeading,
  ActionSheetRowFace,
} from '../../../touch/ActionSheet';
import { useTouchShell } from '../../../touch/useTouchShell';
import { workItemTestIds as ids } from '../testIds';
import { useWorkItemsOfType } from '../workItemApi';
import { inputClass } from './inputClasses';

// A Feature that sits under an Epic reads `Epic › Feature`, so two Features with one name can be told apart.
const pathOf = (item: WorkItemDto): string =>
  item.type.id === 'feature' && item.parent?.type.id === 'epic'
    ? `${item.parent.title} › ${item.title}`
    : item.title;

// At touch Features and Epics sit under their Epic's heading (SHIP-714 L714b); the Epics themselves, and Features with
// no Epic, get a heading of their own. Story-level candidates (a task's parents) all sit under a Feature, so they
// have no Epic to group by and get no headings. Groups keep the order their first candidate has and are keyed by the
// Epic's id, so two Epics with one title stay apart.
const groupsEpics = (types: WorkItemTypeId[]): boolean =>
  types.includes('feature') || types.includes('epic');

const groupOf = (item: WorkItemDto): { id: string; label: string } =>
  item.type.id === 'epic'
    ? { id: 'epics', label: 'Epics' }
    : item.parent?.type.id === 'epic'
      ? { id: item.parent.id, label: item.parent.title }
      : { id: 'no-epic', label: 'No epic' };

const footnoteOf = (types: WorkItemTypeId[]): string =>
  types.length === 1 && types[0] === 'feature'
    ? 'A user story, bug or action item sits under a Feature.'
    : types.includes('epic')
      ? 'A Feature sits under an Epic or another Feature.'
      : 'A task sits under a user story, bug or action item.';

type Group = { id: string; label: string; items: WorkItemDto[] };

function groupedBy(items: WorkItemDto[], byEpic: boolean): Group[] {
  if (!byEpic) return [{ id: 'all', label: '', items }];
  const groups = new Map<string, Group>();
  for (const item of items) {
    const { id, label } = groupOf(item);
    const group = groups.get(id) ?? { id, label, items: [] };
    group.items.push(item);
    groups.set(id, group);
  }
  return [...groups.values()];
}

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
  // At touch the search sits in a sheet and is not focused until tapped, so no keyboard covers the list (C4).
  const { touch } = useTouchShell();
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
        autoFocus={!touch}
        data-testid={ids.parentSearch}
        aria-label="Search parents"
        placeholder="Search by title or key"
        className={`${inputClass} m-1 touch:mx-0 touch:h-10`}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <button
        type="button"
        data-testid={ids.parentOption}
        role="menuitemradio"
        aria-checked={value === null}
        className="h-[30px] touch:h-[var(--touch-target)] rounded-control px-2 text-left text-[length:var(--text-sm)] text-ink-muted hover:bg-signal-soft"
        onClick={() => onPick(null)}
      >
        No parent
      </button>
      {touch ? (
        <>
          {groupedBy(shown, groupsEpics(types)).map((group, index) => (
            <Fragment key={group.id}>
              <ActionSheetGroupHeading label={group.label} divider={index > 0} />
              {group.items.map((item) => {
                const current = value?.id === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    data-testid={ids.parentOption}
                    role="menuitemradio"
                    aria-checked={current}
                    aria-current={current ? 'true' : undefined}
                    className={`${actionSheetRowClass} gap-3 text-ink ${current ? actionSheetCheckedClass : ''}`}
                    onClick={() =>
                      onPick({ id: item.id, key: item.key, type: item.type, title: item.title })
                    }
                  >
                    <ActionSheetRowFace
                      face={{
                        label: item.title,
                        icon: <TypeIcon type={item.type.id} />,
                        detail: item.key,
                        checked: current,
                      }}
                    />
                  </button>
                );
              })}
            </Fragment>
          ))}
          <ActionSheetFootnote>{footnoteOf(types)}</ActionSheetFootnote>
        </>
      ) : (
        shown.map((item) => (
          <button
            key={item.id}
            type="button"
            data-testid={ids.parentOption}
            role="menuitemradio"
            aria-checked={value?.id === item.id}
            className="flex h-[30px] touch:h-[var(--touch-target)] items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
            onClick={() =>
              onPick({ id: item.id, key: item.key, type: item.type, title: item.title })
            }
          >
            <TypeIcon type={item.type.id} />
            <span className="truncate">{pathOf(item)}</span>
            <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">{item.key}</span>
          </button>
        ))
      )}
    </>
  );
}
