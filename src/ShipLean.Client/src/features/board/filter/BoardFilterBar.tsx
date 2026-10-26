import { useState } from 'react';
import type { Level } from '../../../routes';
import { useProjectMembers, useProjectSprints } from '../../workItem/workItemApi';
import type { BoardFilterQuery } from '../boardApi';
import { boardTestIds as ids } from '../testIds';
import { EpicFilter } from './EpicFilter';
import { KeywordInput } from './KeywordInput';
import { PeopleFilter } from './PeopleFilter';
import { SprintFilter } from './SprintFilter';
import { filterCount, toggled } from './filterQuery';
import { useEpicOptions } from './useEpicOptions';

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
  const members = useProjectMembers(projectId).data ?? [];
  const sprints = useProjectSprints(projectId).data ?? [];
  const epics = useEpicOptions(projectId, level === 'features');
  const people = members.map((member) => member.user);
  return (
    <div
      data-testid={ids.filterBar}
      className="flex flex-none flex-wrap items-center gap-2 border-b border-edge py-2"
    >
      <KeywordInput
        key={clears}
        value={filter.q ?? ''}
        onChange={(q) => onChange({ ...filter, q: q === '' ? undefined : q })}
      />
      {level === 'stories' ? (
        <>
          <PeopleFilter
            label="People"
            noneLabel="Unassigned"
            people={people}
            chosen={filter.assignee ?? []}
            onToggle={(id) => onChange({ ...filter, assignee: toggled(filter.assignee, id) })}
          />
          <SprintFilter
            sprints={sprints}
            value={filter.sprint}
            onChange={(sprint) => onChange({ ...filter, sprint })}
          />
        </>
      ) : (
        <>
          <PeopleFilter
            label="Owner"
            noneLabel="No owner"
            people={people}
            chosen={filter.owner ?? []}
            onToggle={(id) => onChange({ ...filter, owner: toggled(filter.owner, id) })}
          />
          {level === 'features' ? (
            <EpicFilter
              epics={epics}
              value={filter.epic}
              onChange={(epic) => onChange({ ...filter, epic })}
            />
          ) : null}
        </>
      )}
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
