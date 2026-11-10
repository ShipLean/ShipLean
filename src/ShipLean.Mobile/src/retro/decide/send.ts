// Senders for newAction, addToAction and notNeeded (RT3b).
import { listProjects } from '@shiplean/core/api/projects';
import {
  addPointsToRetroAction,
  fetchRetroAction,
  fetchRetroActionDialog,
  markPointNotNeeded,
  newRetroAction,
  undoPointNotNeeded,
} from '@shiplean/core/api/retrosApi';
import type { RetroFixType } from '@shiplean/core/api/retros';
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

// A new action item is two writes (E19): the create takes no owner, so the owner is set on the item it made. The create
// is not repeated if the assign fails: the error reaches the runner as it is.
export async function sendDecision(a: DecisionAction, api: Api): Promise<void> {
  void api;
  switch (a.kind) {
    case 'newAction': {
      const made = await newRetroAction(a.projectId, {
        title: a.title,
        fixType: DEFAULT_FIX,
        pointIds: a.pointIds,
        themeId: a.themeId,
        themeName: null,
      });
      if (a.ownerId !== null) {
        const item = await getItemByKey(a.projectId, made.action.key);
        await patchItem(made.action.workItemId, item.revision, assignTo(a.ownerId));
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

// An uncertain send went through when the points are no longer open to decide. The action dialog read lists a project's
// undecided points, so a point it still lists was not decided; one it does not list is decided. An action that gave no
// project (Not needed) is looked for in every project of the session. An addToAction is checked on its action item.
export async function reconcileDecision(
  items: Pending[],
  api: Api,
): Promise<('sent' | 'notSent')[]> {
  void api;
  const actions = items.map((p) => p.action as DecisionAction);
  const open = new Map<string, Promise<Set<string>>>();
  const openIn = (projectId: string, pointIds: string[]) => {
    const key = `${projectId}:${[...pointIds].sort().join(',')}`;
    let read = open.get(key);
    if (read === undefined) {
      read = fetchRetroActionDialog(projectId, { pointIds }).then(
        (d) => new Set(d.selected.map((point) => point.id)),
      );
      open.set(key, read);
    }
    return read;
  };
  const noProject = actions.filter((a) => a.kind === 'notNeeded');
  const projectIds =
    noProject.length === 0 ? [] : (await listProjects()).map((project) => project.id);
  const stillOpenAnywhere = new Set<string>();
  if (noProject.length > 0) {
    const ids = noProject.map((a) => (a as { pointId: string }).pointId);
    for (const found of await Promise.all(projectIds.map((id) => openIn(id, ids))))
      for (const id of found) stillOpenAnywhere.add(id);
  }
  return Promise.all(
    actions.map(async (a): Promise<'sent' | 'notSent'> => {
      switch (a.kind) {
        case 'newAction': {
          const stillOpen = await openIn(a.projectId, a.pointIds);
          return a.pointIds.every((id) => !stillOpen.has(id)) ? 'sent' : 'notSent';
        }
        case 'addToAction': {
          const detail = await fetchRetroAction(a.itemId);
          const linked = new Set(detail.points.map((point) => point.id));
          return a.pointIds.every((id) => linked.has(id)) ? 'sent' : 'notSent';
        }
        case 'notNeeded':
          return stillOpenAnywhere.has(a.pointId) ? 'notSent' : 'sent';
      }
    }),
  );
}
