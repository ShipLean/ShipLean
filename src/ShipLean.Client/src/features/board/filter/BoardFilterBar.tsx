import { useState } from 'react';
import type { Level } from '../../../routes';
import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { BoardFilterFields } from './BoardFilterFields';
import { filterCount } from './filterQuery';

// The filter row under the toolbar. Stories: keyword, people (or Unassigned), sprint. Features: keyword, owner (or
// No owner), Epic. Epics: keyword, owner. The query goes to the server, which hides cards before it truncates Done.
export function BoardFilterBar({
  projectId,
  level,
  filter,
  onChange,
}: {
  projectId: string;
  level: Level;
  filter: BoardFilterQuery;
  onChange: (filter: BoardFilterQuery) => void;
}) {
  const [clears, setClears] = useState(0);
  return (
    <div
      data-testid={ids.filterBar}
      className="flex flex-none flex-wrap items-center gap-2 border-b border-edge py-2"
    >
      <BoardFilterFields
        projectId={projectId}
        level={level}
        filter={filter}
        onChange={onChange}
        keywordKey={clears}
      />
      <button
        type="button"
        data-testid={ids.filterClear}
        className="btn-ghost"
        disabled={filterCount(filter) === 0}
        onClick={() => {
          setClears((count) => count + 1);
          onChange({});
        }}
      >
        Clear
      </button>
    </div>
  );
}
