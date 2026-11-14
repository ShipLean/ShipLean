import { useState } from 'react';
import type { CriteriaJson } from '@shiplean/core/api/persisted';
import { BacklogSearchBox } from '../search/BacklogSearchBox';
import { useBacklogSearch } from '../search/useBacklogSearch';
import { ListCommandsProvider } from '../commands/listCommands';
import { FilterButton } from '../filter/FilterButton';
import { FilterPanel } from '../filter/FilterPanel';
import { SelectionProvider } from '../selection/SelectionProvider';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import { itemRow } from './fixtures';

const NONE: CriteriaJson = { match: 'all', clauses: [] };

function Toolbar({
  level,
  withActions,
  sorted,
}: {
  level: 'stories' | 'features' | 'epics';
  withActions: boolean;
  sorted: boolean;
}) {
  const search = useBacklogSearch();
  const [completed, setCompleted] = useState(false);
  const [criteria, setCriteria] = useState(NONE);
  const [filterOpen, setFilterOpen] = useState(false);
  const [sort, setSort] = useState(sorted);
  const [log, setLog] = useState<string[]>([]);
  const rows = ['SHIP-158', 'SHIP-160'].filter(
    (key) => search.apply([itemRow({ key })]).length > 0,
  );
  return (
    <>
      <BacklogToolbar
        projectKey="SHIP"
        level={level}
        mode="list"
        modes={level === 'epics' ? [] : level === 'features' ? ['list', 'tree'] : undefined}
        newItemLabel={
          level === 'features' ? '+ New Feature' : level === 'epics' ? '+ New Epic' : undefined
        }
        onNewItem={() => undefined}
        includeCompleted={completed}
        onIncludeCompletedChange={setCompleted}
        columns={{
          options: [{ id: 'key', label: 'Key', required: true }],
          hidden: [],
          onChange: () => undefined,
        }}
        keys={() => (
          <button type="button" data-testid="harness-keys">
            Keys
          </button>
        )}
        search={() => <BacklogSearchBox search={search} />}
        options={
          withActions
            ? () => (
                <button
                  type="button"
                  data-testid="harness-option"
                  onClick={() => setLog([...log, 'option'])}
                >
                  Add many
                </button>
              )
            : undefined
        }
        moreActions={
          withActions
            ? [
                { id: 'select', label: 'Select', onSelect: () => setLog([...log, 'select']) },
                { id: 'estimate', label: 'Estimate', onSelect: () => setLog([...log, 'estimate']) },
              ]
            : undefined
        }
        sort={sort ? { label: 'Points, ascending', onClear: () => setSort(false) } : undefined}
        filter={() => (
          <FilterButton
            criteria={criteria}
            open={filterOpen}
            onToggle={() => setFilterOpen(!filterOpen)}
            onRemoveClause={(index) =>
              setCriteria({ ...criteria, clauses: criteria.clauses.filter((_, i) => i !== index) })
            }
            lists={{}}
          />
        )}
      />
      {filterOpen ? (
        <FilterPanel
          criteria={criteria}
          onChange={setCriteria}
          error={null}
          lists={{}}
          onClose={() => setFilterOpen(false)}
        />
      ) : null}
      <span data-testid="harness-state">{`${completed}|${sort}|${log.join(',')}|${rows.join(',')}`}</span>
      <span data-testid="harness-criteria">{JSON.stringify(criteria)}</span>
    </>
  );
}

// CT-only: the toolbar with every slot filled (filter, search, options, More actions, a sort), to look at on a phone.
export function ToolbarTouchHarness({
  level = 'stories',
  withActions = false,
  sorted = false,
}: {
  level?: 'stories' | 'features' | 'epics';
  withActions?: boolean;
  sorted?: boolean;
}) {
  return (
    <ListCommandsProvider>
      <SelectionProvider>
        <Toolbar level={level} withActions={withActions} sorted={sorted} />
      </SelectionProvider>
    </ListCommandsProvider>
  );
}
