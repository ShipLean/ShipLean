import type { BacklogSearch } from './useBacklogSearch';

// The search field in the toolbar.
// TODO: the field replaces this empty one.
export function BacklogSearchBox({ search }: { search: BacklogSearch }) {
  void search;
  return null;
}
