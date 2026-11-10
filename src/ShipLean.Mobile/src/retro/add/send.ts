// Senders for addPoint, support and unsupport (RT2).
import type { Api, Pending, PreviewLine, SendReceipt } from '../../actions/types';
import type { PointAction } from '../actions';

export async function sendPoint(a: PointAction, api: Api): Promise<SendReceipt | void> {
  // TODO: post the point (createRetroPoint), or supportPoint / unsupportPoint, through the core.
  void api;
  throw new Error(`Sending ${a.kind} is not built yet.`);
}

export function describePoint(a: PointAction): PreviewLine {
  // TODO: word each kind as the design's preview rows do.
  return { id: `retro:${a.kind}`, icon: 'dot', text: a.kind, removable: true };
}

export async function undoPoint(
  a: PointAction,
  api: Api,
  receipt?: SendReceipt | null,
): Promise<void> {
  void receipt;
  // TODO: archive the new point (archiveRetroPoint), or reverse the support.
  void api;
  throw new Error(`Undoing ${a.kind} is not built yet.`);
}

export async function reconcilePoint(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  // TODO: reload the sprint retro and say, per pending, whether the point or support is there.
  void api;
  return items.map(() => 'notSent');
}
