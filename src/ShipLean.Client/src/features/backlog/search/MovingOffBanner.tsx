import type { CheckQueue } from '../strip/useCheckQueue';
import type { BacklogSearch } from './useBacklogSearch';

// Planning's banner that moving rows is off while a search or a check queue narrows them.
// TODO: the banner replaces this empty one.
export function MovingOffBanner({ search, queue }: { search: BacklogSearch; queue: CheckQueue }) {
  void [search, queue];
  return null;
}
