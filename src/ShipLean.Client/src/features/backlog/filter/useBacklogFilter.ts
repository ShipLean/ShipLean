import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { CriteriaJson } from '../../../api/persisted';
import type { ProjectDto } from '../../../api/projects';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { newClause } from '../../../criteria/defaultClause';
import type { CriteriaClause } from '../../../api/persisted';
import type { Level } from '../../../routes';
import {
  backlogKey,
  BACKLOG_COLUMNS,
  useBacklog,
  useBacklogView,
  type ColumnSpec,
} from '../backlogApi';
import { filterParamOf, isFiltering, NO_CRITERIA } from './filterParam';
import { useFilterLists } from './useFilterLists';

const NO_ROWS: BacklogRowDto[] = [];

/** What differs between the backlogs that share this hook: the level, its saved columns and the clause a fresh filter starts with. */
export interface BacklogFilterOptions {
  level: Level;
  columns: readonly ColumnSpec[];
  defaultClause: (lists: ReturnType<typeof useFilterLists>) => CriteriaClause;
}

const STORIES: BacklogFilterOptions = {
  level: 'stories',
  columns: BACKLOG_COLUMNS,
  defaultClause: (lists) => ({ ...newClause('type', lists), value: 'bug' }),
};

/**
 * What the backlogs' list and tree modes share (the Stories ones by default): the Completed toggle, the new-item row, the filter panel's
 * clauses, the read of the backlog, and the saved columns. The filter sent is the last whole one (a clause still being
 * typed leaves the rows as they were), and the rows are those of the last read that worked, so a filter the server
 * refuses leaves them as they were too.
 */
export function useBacklogFilter(
  project: ProjectDto,
  mode: 'list' | 'tree',
  { level, columns, defaultClause }: BacklogFilterOptions = STORIES,
) {
  const [includeCompleted, setIncludeCompleted] = useState(false);
  const [adding, setAddingOpen] = useState(false);
  const [addType, setAddType] = useState<WorkItemTypeId>('story');
  // Closing the row forgets a type a link picked, so the toolbar button opens it on User Story again.
  const setAdding = (open: boolean) => {
    setAddingOpen(open);
    if (!open) setAddType('story');
  };
  // Opens the row with a type picked (or switches the open one).
  const openAdd = (type: WorkItemTypeId) => {
    setAddType(type);
    setAddingOpen(true);
  };
  const [criteria, setCriteria] = useState<CriteriaJson>(NO_CRITERIA);
  const [filterOpen, setFilterOpen] = useState(false);
  const [filter, setFilter] = useState('');
  const whole = filterParamOf(criteria);
  const sent = criteria.clauses.length === 0 ? '' : whole === '' ? filter : whole;
  if (sent !== filter) setFilter(sent);
  const filtering = isFiltering(criteria);
  const lists = useFilterLists(project.id);
  const view = useBacklogView(level, columns);
  const backlog = useBacklog({
    projectId: project.id,
    level,
    mode,
    filter: sent,
    includeCompleted,
  });
  const queryKey = backlogKey(project.id, level, mode, sent, includeCompleted);

  const [read, setRead] = useState<BacklogViewDto | undefined>(undefined);
  if (backlog.isSuccess && backlog.data !== read) setRead(backlog.data);

  const refused =
    backlog.error instanceof ApiRequestError && backlog.error.code === 'validation' && filtering
      ? backlog.error.message
      : null;

  const toggleFilter = () => {
    if (!filterOpen && criteria.clauses.length === 0) {
      setCriteria({ ...criteria, clauses: [defaultClause(lists)] });
    }
    setFilterOpen(!filterOpen);
  };

  return {
    specs: columns,
    includeCompleted,
    setIncludeCompleted,
    adding,
    addType,
    setAdding,
    openAdd,
    criteria,
    setCriteria,
    filterOpen,
    toggleFilter,
    filtering,
    lists,
    view,
    backlog,
    queryKey,
    /** The rows of the last read that worked. */
    rows: read?.rows ?? NO_ROWS,
    /** The server's message for a filter it refused, or null. */
    refused,
  };
}

export type BacklogFilterState = ReturnType<typeof useBacklogFilter>;
