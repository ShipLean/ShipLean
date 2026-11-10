// Senders for newAction, addToAction and notNeeded (RT3b).
import {
  addPointsToRetroAction,
  fetchRetroAction,
  fetchSprintRetro,
  markPointNotNeeded,
  newRetroAction,
  undoPointNotNeeded,
} from '@shiplean/core/api/retrosApi';
import type {
  RetroActionRefDto,
  RetroFixType,
  RetroPointDto,
  SprintRetroDto,
} from '@shiplean/core/api/retros';
import { getItemByKey, patchItem } from '@shiplean/core/api/workItem';
import type { WorkItemChanges } from '@shiplean/core/api/workItems';
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { DecisionAction } from '../actions';

// The web's default fix type (DEFAULT_FIX in RetroActionForm.tsx). Main requires one; the phone leaves it out.
const DEFAULT_FIX: RetroFixType = 'guide';
// Main refuses an empty reason; the person may give none (P-R4).
const NO_REASON = 'Not needed';

const assignTo = (ownerId: string): WorkItemChanges => ({
  title: null,
  description: null,
  acceptanceCriteria: null,
  reproSteps: null,
  severity: null,
  state: null,
  assignee: { value: ownerId },
  sprint: null,
  priority: null,
  points: null,
  parent: null,
  tags: null,
  addLinks: [],
  removeLinkIds: [],
});

// The points of a sprint retro, by id.
const pointsOf = (retro: SprintRetroDto, ids: string[]): RetroPointDto[] => {
  const all = retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
  return ids.flatMap((id) => all.filter((p) => p.id === id));
};

// The action item every point already carries, or null when any point has none.
const madeActionOf = (retro: SprintRetroDto, ids: string[]): RetroActionRefDto | null => {
  const points = pointsOf(retro, ids);
  if (points.length !== ids.length) return null;
  const refs = points.map((p) => p.action);
  return refs.every((r) => r !== null) ? refs[0] : null;
};

// A new action item is two writes (E19): the create takes no owner, so the owner is set on the item it made. The send
// is resumable: when the points already carry an action item (an earlier send created it and the assign failed), the
// create is skipped and only the assign is done, so a retry never makes a second action item.
export async function sendDecision(a: DecisionAction, api: Api): Promise<void> {
  void api;
  switch (a.kind) {
    case 'newAction': {
      const existing = madeActionOf(await fetchSprintRetro(a.sprintId), a.pointIds);
      const made =
        existing ??
        (
          await newRetroAction(a.projectId, {
            title: a.title,
            fixType: DEFAULT_FIX,
            pointIds: a.pointIds,
            themeId: a.themeId,
            themeName: null,
          })
        ).action;
      if (a.ownerId !== null) {
        const item = await getItemByKey(a.projectId, made.key);
        await patchItem(made.workItemId, item.revision, assignTo(a.ownerId));
      }
      return;
    }
    case 'addToAction':
      await addPointsToRetroAction(a.itemId, { pointIds: a.pointIds });
      return;
    case 'notNeeded':
      await markPointNotNeeded(a.pointId, {
        reason: a.reason.trim() === '' ? NO_REASON : a.reason,
      });
  }
}

export function describeDecision(a: DecisionAction): PreviewLine {
  switch (a.kind) {
    case 'newAction':
      return {
        id: `retro:newAction:${a.pointIds.join(',')}`,
        icon: 'plus',
        text: `New action item "${a.title}"${a.ownerName === null ? '' : ` · ${a.ownerName}`}`,
        removable: true,
      };
    case 'addToAction':
      return {
        id: `retro:addToAction:${a.key}:${a.pointIds.join(',')}`,
        icon: 'plus',
        text: `Add to ${a.key}`,
        removable: true,
      };
    case 'notNeeded':
      return {
        id: `retro:notNeeded:${a.pointId}`,
        icon: 'dot',
        text: a.reason.trim() === '' ? 'Not needed' : `Not needed · "${a.reason}"`,
        removable: true,
      };
  }
}

// Only Not needed has a reverse. A new action item and added points stay (the executor's reversible says so).
export async function undoDecision(a: DecisionAction, api: Api): Promise<void> {
  void api;
  if (a.kind !== 'notNeeded') throw new Error(`${a.kind} has no undo.`);
  await undoPointNotNeeded(a.pointId);
}

// An uncertain send is settled from the sprint retro, read once per sprint. A newAction went through when its points
// carry an action item and, with an owner, that item is assigned to the owner (otherwise the resumable send finishes
// the assign). A notNeeded went through when the point is decided not needed. An addToAction is checked on its item.
export async function reconcileDecision(
  items: Pending[],
  api: Api,
): Promise<('sent' | 'notSent')[]> {
  void api;
  const actions = items.map((p) => p.action as DecisionAction);
  const retros = new Map<string, Promise<SprintRetroDto>>();
  const retroOf = (sprintId: string) => {
    let read = retros.get(sprintId);
    if (read === undefined) {
      read = fetchSprintRetro(sprintId);
      retros.set(sprintId, read);
    }
    return read;
  };
  return Promise.all(
    actions.map(async (a): Promise<'sent' | 'notSent'> => {
      switch (a.kind) {
        case 'newAction': {
          const retro = await retroOf(a.sprintId);
          const made = madeActionOf(retro, a.pointIds);
          if (made === null) return 'notSent';
          if (a.ownerId === null) return 'sent';
          const row = retro.actionItems.find((r) => r.action.workItemId === made.workItemId);
          return row?.owner?.id === a.ownerId ? 'sent' : 'notSent';
        }
        case 'addToAction': {
          const detail = await fetchRetroAction(a.itemId);
          const linked = new Set(detail.points.map((point) => point.id));
          return a.pointIds.every((id) => linked.has(id)) ? 'sent' : 'notSent';
        }
        case 'notNeeded': {
          const [point] = pointsOf(await retroOf(a.sprintId), [a.pointId]);
          return point?.decision === 'notNeeded' ? 'sent' : 'notSent';
        }
      }
    }),
  );
}
