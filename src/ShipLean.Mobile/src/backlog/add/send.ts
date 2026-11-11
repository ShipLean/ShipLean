import {
  addItems,
  getBacklog,
  type BacklogFilter,
  type BacklogLevel,
} from '@shiplean/core/api/backlog';
import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';
import { getStates } from '@shiplean/core/api/projectMeta';
import { patchItem } from '@shiplean/core/api/workItem';
import type { WorkItemChanges } from '@shiplean/core/api/workItems';
import type { Api, Pending, PreviewLine, SendReceipt } from '../../actions/types';
import type { BacklogAddItem } from '../actions';

// The top of the backlog, which is where a typed add and a captured Story land unless "at the bottom" is said.
export const TOP: BulkPlacement = { slot: 'first', beforeItemId: null };

const TYPE_LABEL: Record<BacklogAddItem['type'], string> = {
  story: 'Story',
  bug: 'Bug',
  feature: 'Feature',
};

// The level a new item of this type is listed on.
export function levelOf(type: BacklogAddItem['type']): BacklogLevel {
  return type === 'feature' ? 'features' : 'stories';
}

// The whole level, closed rows included.
const LEVEL: BacklogFilter = { mode: 'list', filter: '', includeCompleted: true };

const NO_CHANGES: WorkItemChanges = {
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: null,
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
};

// The calls go through @shiplean/core, so the runner's api argument is unused here.
// One item, no sprint, points or tasks; the server gives it its type's first Proposed state.
export async function sendAddItem(a: BacklogAddItem, api: Api): Promise<SendReceipt> {
  void api;
  const result = await addItems(
    a.projectId,
    [
      {
        type: a.type,
        title: a.title,
        description: null,
        points: null,
        parentId: a.parentId,
        tasks: [],
      },
    ],
    a.placement ?? TOP,
    null,
  );
  const [created] = result.created;
  if (created === undefined) throw new Error('The server added nothing.');
  return { createdId: created.id };
}

// `New Story: "title" · under SHIP-906 · at the top`. The title is shown as it was typed or spoken.
export function describeAddItem(a: BacklogAddItem): PreviewLine {
  const top = a.placement === null || a.placement.slot === 'first';
  const where = [
    ...(a.parentKey === null ? [] : [`under ${a.parentKey}`]),
    top ? 'at the top' : 'at the bottom',
  ];
  return {
    id: `addItem:${a.title}`,
    icon: 'plus',
    text: `New ${TYPE_LABEL[a.type]}: "${a.title}" · ${where.join(' · ')}`,
    detail: top ? 'Everything else moves down one.' : undefined,
    removable: true,
  };
}

// Moves the item this send created to its type's Removed state, at the revision it has now. Never a delete.
export async function undoAddItem(
  a: BacklogAddItem,
  api: Api,
  receipt?: SendReceipt | null,
): Promise<void> {
  void api;
  if (receipt === undefined || receipt === null) {
    throw new Error("The added item is not known, so the add can't be undone.");
  }
  const [view, states] = await Promise.all([
    getBacklog(a.projectId, levelOf(a.type), LEVEL),
    getStates(a.projectId),
  ]);
  const row = view.rows.find((r) => r.id === receipt.createdId);
  if (row === undefined) throw new Error(`"${a.title}" is not on the backlog any more.`);
  const removed = states.find((s) => s.type === a.type && s.category === 'removed');
  if (removed === undefined) throw new Error(`There is no Removed state for a ${a.type}.`);
  await patchItem(row.id, row.revision, { ...NO_CHANGES, state: { value: removed.id } });
}

// An uncertain add went through when the level has a row with exactly that title under that parent. Each level is read once.
export async function reconcileAddItem(
  items: Pending[],
  api: Api,
): Promise<('sent' | 'notSent')[]> {
  void api;
  const levels = new Map<string, ReturnType<typeof getBacklog>>();
  return Promise.all(
    items.map(async ({ action }) => {
      const a = action as BacklogAddItem;
      const scope = `${a.projectId}|${levelOf(a.type)}`;
      const read = levels.get(scope) ?? getBacklog(a.projectId, levelOf(a.type), LEVEL);
      levels.set(scope, read);
      const { rows } = await read;
      return rows.some((r) => r.title === a.title && (r.parent?.id ?? null) === a.parentId)
        ? 'sent'
        : 'notSent';
    }),
  );
}
