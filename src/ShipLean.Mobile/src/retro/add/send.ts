// Senders for addPoint, support and unsupport (RT2).
import { getItemByKey } from '@shiplean/core/api/workItem';
import { getMe } from '@shiplean/core/api/me';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import {
  archiveRetroPoint,
  createRetroPoint,
  fetchSprintRetro,
  fetchWorkItemRetros,
  supportPoint,
  unsupportPoint,
} from '@shiplean/core/api/retrosApi';
import type { Api, Pending, PreviewLine, SendReceipt } from '../../actions/types';
import type { PointAction } from '../actions';
import { CATEGORY_LABELS } from '../notes/notesModel';

type AddPoint = Extract<PointAction, { kind: 'addPoint' }>;

// The item the point is about: its id, or its key looked up. An unknown key is the core's 404, never the sprint.
async function itemOf(a: AddPoint): Promise<string | null> {
  if (a.itemId !== null) return a.itemId;
  if (a.key !== null) return (await getItemByKey(a.projectId, a.key)).id;
  return null;
}

// addPoint resolves { createdId: the new point's id }; support and unsupport resolve nothing (both are idempotent).
export async function sendPoint(a: PointAction, api: Api): Promise<SendReceipt | void> {
  void api;
  if (a.kind !== 'addPoint')
    return a.kind === 'support' ? supportPoint(a.pointId) : unsupportPoint(a.pointId);
  const workItemId = await itemOf(a);
  if (workItemId === null && a.sprintId === null)
    throw new Error('A point is about an item or a sprint.');
  const point = await createRetroPoint(a.projectId, {
    retroId: null,
    workItemId,
    sprintId: workItemId === null ? a.sprintId : null,
    category: a.category,
    text: a.text.trim(),
    rootCause: null,
    themeId: null,
    themeName: null,
  });
  return { createdId: point.id };
}

export function describePoint(a: PointAction): PreviewLine {
  if (a.kind !== 'addPoint')
    return {
      id: `retro:${a.kind}:${a.pointId}`,
      icon: 'plus',
      text: a.kind === 'support' ? '+1 on a point' : 'Took back +1 on a point',
      removable: true,
    };
  const about = a.key !== null ? ` · about ${a.key}` : ' · about this sprint';
  return {
    id: `retro:addPoint:${a.text}`,
    icon: 'note',
    text: `${CATEGORY_LABELS[a.category]}: "${a.text}"${about}`,
    ...(a.categoryGuessed ? { detail: 'Category picked for you' } : {}),
    removable: true,
  };
}

// Undo reverses what the send did: a +1 with a -1 and back, a new point by archiving it (no hard deletes). A new
// point needs its receipt; without one the undo fails rather than guess.
export async function undoPoint(
  a: PointAction,
  api: Api,
  receipt?: SendReceipt | null,
): Promise<void> {
  void api;
  if (a.kind !== 'addPoint')
    return a.kind === 'support' ? unsupportPoint(a.pointId) : supportPoint(a.pointId);
  if (receipt === undefined || receipt === null)
    throw new Error('The point to undo is not known: no receipt.');
  return archiveRetroPoint(receipt.createdId);
}

// The points a retro read holds for an add: the sprint's retro, or the item's cards.
async function pointsFor(a: AddPoint): Promise<RetroPointDto[]> {
  if (a.sprintId !== null && a.key === null && a.itemId === null)
    return (await fetchSprintRetro(a.sprintId)).categories.flatMap((c) =>
      c.groups.flatMap((g) => g.points),
    );
  const itemId = await itemOf(a);
  if (itemId === null) return [];
  return (await fetchWorkItemRetros(itemId)).cards.flatMap((c) => c.points);
}

// An uncertain addPoint went through when the retro now holds a point by you with exactly that text and category.
// +1 and -1 are idempotent, so a resend is safe and they say notSent.
export async function reconcilePoint(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  const adding = items.some((p) => (p.action as PointAction).kind === 'addPoint');
  const me = adding ? (await getMe()).id : null;
  return Promise.all(
    items.map(async (p): Promise<'sent' | 'notSent'> => {
      const a = p.action as PointAction;
      if (a.kind !== 'addPoint') return 'notSent';
      const points = await pointsFor(a);
      return points.some(
        (pt) => pt.author.user.id === me && pt.category === a.category && pt.text === a.text.trim(),
      )
        ? 'sent'
        : 'notSent';
    }),
  );
}
