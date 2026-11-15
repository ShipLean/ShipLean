import type { ReactNode } from 'react';
import type { Level } from '../../../routes';
import { useProjectMembers, useProjectSprints } from '../../workItem/workItemApi';
import type { BoardFilterQuery } from '../boardApi';
import { EpicFilter } from './EpicFilter';
import { KeywordInput } from './KeywordInput';
import { PeopleFilter } from './PeopleFilter';
import { SprintFilter } from './SprintFilter';
import { toggled } from './filterQuery';
import { useEpicOptions } from './useEpicOptions';

// A control with its label above it in the Filter sheet; as it is in the bar, it stands alone.
function Labelled({ label, children }: { label: string | null; children: ReactNode }) {
  if (label === null) return <>{children}</>;
  return (
    <div className="grid gap-1">
      <span className="text-[length:var(--text-xs)] font-medium text-ink-muted">{label}</span>
      {children}
    </div>
  );
}

// The filter's controls, shared by the bar (desktop) and the Filter sheet (touch). Stories: keyword, people (or
// Unassigned), sprint. Features: keyword, owner (or No owner), Epic. Epics: keyword, owner. `labelled` puts a label
// over each control, `keywordClass` sizes the keyword box, `onKeywordDraft` hears every keystroke.
export function BoardFilterFields({
  projectId,
  level,
  filter,
  onChange,
  keywordKey,
  labelled = false,
  keywordClass,
  onKeywordDraft,
}: {
  projectId: string;
  level: Level;
  filter: BoardFilterQuery;
  onChange: (filter: BoardFilterQuery) => void;
  /** Remounts the keyword box (a cleared filter empties it). */
  keywordKey: number;
  labelled?: boolean;
  keywordClass?: string;
  onKeywordDraft?: (text: string) => void;
}) {
  const members = useProjectMembers(projectId).data ?? [];
  const sprints = useProjectSprints(projectId).data ?? [];
  const epics = useEpicOptions(projectId, level === 'features');
  const people = members.map((member) => member.user);
  const label = (text: string) => (labelled ? text : null);
  return (
    <>
      <Labelled label={label('Keyword')}>
        <KeywordInput
          key={keywordKey}
          value={filter.q ?? ''}
          className={keywordClass}
          onDraft={onKeywordDraft}
          onChange={(q) => onChange({ ...filter, q: q === '' ? undefined : q })}
        />
      </Labelled>
      {level === 'stories' ? (
        <>
          <Labelled label={label('People')}>
            <PeopleFilter
              label="People"
              noneLabel="Unassigned"
              people={people}
              chosen={filter.assignee ?? []}
              onToggle={(id) => onChange({ ...filter, assignee: toggled(filter.assignee, id) })}
            />
          </Labelled>
          <Labelled label={label('Sprint')}>
            <SprintFilter
              sprints={sprints}
              value={filter.sprint}
              onChange={(sprint) => onChange({ ...filter, sprint })}
            />
          </Labelled>
        </>
      ) : (
        <>
          <Labelled label={label('Owner')}>
            <PeopleFilter
              label="Owner"
              noneLabel="No owner"
              people={people}
              chosen={filter.owner ?? []}
              onToggle={(id) => onChange({ ...filter, owner: toggled(filter.owner, id) })}
            />
          </Labelled>
          {level === 'features' ? (
            <Labelled label={label('Epic')}>
              <EpicFilter
                epics={epics}
                value={filter.epic}
                onChange={(epic) => onChange({ ...filter, epic })}
              />
            </Labelled>
          ) : null}
        </>
      )}
    </>
  );
}
