import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { BacklogAddItem } from '../actions';

export async function sendAddItem(a: BacklogAddItem, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: post the new item with its placement');
}

export function describeAddItem(a: BacklogAddItem): PreviewLine {
  // TODO: the design's wording ("New Story in SHIP-906").
  return {
    id: `addItem:${a.title}`,
    icon: 'plus',
    text: `New ${a.type}: ${a.title}`,
    removable: true,
  };
}

export async function undoAddItem(a: BacklogAddItem, api: Api): Promise<void> {
  void a;
  void api;
  throw new Error('TODO: move the added item to Removed');
}

export async function reconcileAddItem(
  items: Pending[],
  api: Api,
): Promise<('sent' | 'notSent')[]> {
  void api;
  // TODO: look for the title under its parent.
  return items.map(() => 'notSent');
}
