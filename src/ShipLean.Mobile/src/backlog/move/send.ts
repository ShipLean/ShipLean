import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { BacklogMove } from '../actions';

export async function sendMove(a: BacklogMove, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: post the bulk placement for the move');
}

export function describeMove(a: BacklogMove): PreviewLine {
  // TODO: the design's wording ("SHIP-906 moved from 4 to 2").
  return {
    id: `move:${a.itemId}`,
    icon: 'rank',
    text: `Move ${a.key} from ${a.fromRank} to ${a.toRank}`,
    removable: true,
  };
}

export async function undoMove(a: BacklogMove, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: post the undo placement');
}

export async function reconcileMove(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  // TODO: reload the level and compare each row's place with the move.
  return items.map(() => 'notSent');
}
