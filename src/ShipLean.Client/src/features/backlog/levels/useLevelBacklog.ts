import type { ProjectDto } from '../../../api/projects';
import { newClause } from '../../../criteria/defaultClause';
import { useBacklogFilter } from '../filter/useBacklogFilter';
import { columnsOf, type LevelBacklogLevel } from './levelColumns';

/**
 * What a Features or Epics backlog holds: the shared filter state (Completed toggle, new-item row, filter clauses, the
 * read of the level and its saved columns), starting a fresh filter on tags.
 */
export function useLevelBacklog(project: ProjectDto, level: LevelBacklogLevel) {
  return useBacklogFilter(project, 'list', {
    level,
    columns: columnsOf(level),
    defaultClause: (lists) => newClause('tags', lists),
  });
}
