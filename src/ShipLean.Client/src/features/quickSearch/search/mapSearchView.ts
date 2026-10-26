import type { SearchItemDto, SearchViewDto } from '../../../api/search';
import type { QuickSearchGroup, QuickSearchItem, QuickSearchMatch, QuickSearchRow } from '../types';

export const SEARCH_LIMIT = 8;

export interface SearchMapContext {
  /** The shell's project key, when there is one. */
  currentKey: string | undefined;
  /** Member counts by project key; undefined while the projects load. */
  memberCounts: ReadonlyMap<string, number> | undefined;
  navigate: (url: string) => void;
}

export interface MappedSearch {
  groups: QuickSearchGroup[];
  note: string;
}

// The current project's entries first, the server's order kept within each part.
function currentFirst<T>(
  rows: readonly T[],
  keyOf: (row: T) => string,
  currentKey: string | undefined,
): T[] {
  if (currentKey === undefined) return [...rows];
  return [
    ...rows.filter((row) => keyOf(row) === currentKey),
    ...rows.filter((row) => keyOf(row) !== currentKey),
  ];
}

function matchOf(dto: SearchItemDto): QuickSearchMatch | null {
  switch (dto.matchedIn) {
    case 'description':
    case 'comment':
      return { in: dto.matchedIn, snippet: dto.snippet };
    case 'attachment':
      return { in: 'attachment', name: dto.attachment ?? '', snippet: dto.snippet };
    default:
      return null;
  }
}

function itemRow(dto: SearchItemDto): QuickSearchRow {
  const item: QuickSearchItem = {
    key: dto.key,
    title: dto.title,
    type: dto.type,
    state: dto.state,
    stateCategory: dto.stateCategory,
    parentPath: dto.parentPath,
    projectKey: dto.projectKey,
  };
  return { kind: 'item', id: dto.key, item, match: matchOf(dto) };
}

function membersLabel(count: number | undefined): string {
  if (count === undefined) return '';
  return count === 1 ? '1 member' : `${count} members`;
}

function noteOf(view: SearchViewDto, currentKey: string | undefined): string {
  if (view.more) return `Top ${SEARCH_LIMIT} items · keep typing to narrow`;
  return currentKey ? `All your projects · ${currentKey} first` : 'All your projects';
}

export function mapSearchView(view: SearchViewDto, context: SearchMapContext): MappedSearch {
  const { currentKey, memberCounts, navigate } = context;
  const groups: QuickSearchGroup[] = [
    {
      id: 'items',
      title: 'Work items',
      rows: currentFirst(view.items, (item) => item.projectKey, currentKey).map(itemRow),
    },
    {
      id: 'sprints',
      title: 'Sprints',
      rows: currentFirst(view.sprints, (sprint) => sprint.projectKey, currentKey).map(
        (sprint): QuickSearchRow => ({
          kind: 'sprint',
          id: sprint.url,
          name: sprint.name,
          goal: sprint.goal,
          datesLabel: sprint.datesLabel,
          current: sprint.status === 'current',
          projectKey: sprint.projectKey,
          open: () => navigate(sprint.url),
        }),
      ),
    },
    {
      id: 'projects',
      title: 'Projects',
      rows: view.projects.map((project): QuickSearchRow => ({
        kind: 'project',
        id: project.key,
        key: project.key,
        name: project.name,
        detail:
          project.key === currentKey ? 'current' : membersLabel(memberCounts?.get(project.key)),
        current: project.key === currentKey,
        open: () => navigate(project.url),
      })),
    },
  ];
  return {
    groups: groups.filter((group) => group.rows.length > 0),
    note: noteOf(view, currentKey),
  };
}
