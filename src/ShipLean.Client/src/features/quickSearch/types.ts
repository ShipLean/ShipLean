import type { StateCategory, WorkItemTypeId } from '../../api/refs';

export type QuickSearchStatus = 'ready' | 'loading' | 'error';

/** A work item as a quick search row shows it. */
export interface QuickSearchItem {
  key: string;
  title: string;
  type: WorkItemTypeId;
  state: string;
  stateCategory: StateCategory;
  /** The keys of the parents, from the top down. */
  parentPath: readonly string[];
  projectKey: string;
}

/** Where a result matched, when it was not in the title. */
export type QuickSearchMatch =
  | { in: 'description' | 'comment'; snippet: string }
  | { in: 'attachment'; name: string; snippet: string };

/** A row's id: an item's or project's is its key, a sprint's is its url, an action's is its test id. */
export type QuickSearchRow =
  | { kind: 'item'; id: string; item: QuickSearchItem; match: QuickSearchMatch | null }
  | {
      kind: 'sprint';
      id: string;
      name: string;
      goal: string;
      datesLabel: string;
      current: boolean;
      projectKey: string;
      open: () => void;
    }
  | {
      kind: 'project';
      id: string;
      key: string;
      name: string;
      detail: string;
      current: boolean;
      open: () => void;
    }
  | {
      kind: 'action';
      id: string;
      glyph: string;
      label: string;
      detail: string | null;
      run: () => void;
    };

export type QuickSearchGroupId = 'items' | 'sprints' | 'projects' | 'recent' | 'actions';

export interface QuickSearchGroup {
  id: QuickSearchGroupId;
  title: string;
  rows: readonly QuickSearchRow[];
}

export interface QuickSearchResults {
  groups: readonly QuickSearchGroup[];
  status: QuickSearchStatus;
  /** The footer's status text on the right. */
  note: string | null;
  retry: (() => void) | null;
}
