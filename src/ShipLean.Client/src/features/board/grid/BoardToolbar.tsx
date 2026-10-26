import { useState } from 'react';
import type { BoardViewDto } from '../../../api/views';
import type { Level } from '../../../routes';
import type { BoardFilterQuery } from '../boardApi';
import { BoardFilterBar } from '../filter/BoardFilterBar';
import { FilterNote } from '../filter/FilterNote';
import { FilterToggle } from '../filter/FilterToggle';
import { useBoardLanes } from '../lanes/boardPreferences';
import { LanesSwitch } from '../lanes/LanesSwitch';
import { boardTestIds as ids } from '../testIds';
import { CardsSwitch } from '../toolbar/CardsSwitch';
import { LevelSwitch } from '../toolbar/LevelSwitch';
import { SettingsOpener } from '../toolbar/SettingsOpener';

// The rows over the grid: the level switch, what is open, the lane and Full / Compact choices, Filter and Board
// settings; under them the filter bar while it is open.
export function BoardToolbar({
  view,
  projectId,
  projectKey,
  boardId,
  level,
  compact,
  onCompact,
  filter,
  onFilter,
}: {
  view: BoardViewDto;
  projectId: string;
  projectKey: string;
  boardId: string;
  level: Level;
  compact: boolean;
  onCompact: (compact: boolean) => void;
  filter: BoardFilterQuery;
  onFilter: (filter: BoardFilterQuery) => void;
}) {
  const [filterOpen, setFilterOpen] = useState(false);
  const lanes = useBoardLanes(projectId, boardId);
  return (
    <>
      <div
        data-testid={ids.toolbar}
        className="flex flex-none flex-wrap items-center gap-2 border-b border-edge py-2 text-[length:var(--text-sm)]"
      >
        <LevelSwitch projectKey={projectKey} level={level} />
        <span data-testid={ids.summary} className="text-ink-muted tabular-nums">
          {view.openCount} open · {view.openPoints} pts
        </span>
        <div data-testid={ids.filterSlot} className="flex items-center gap-2">
          <FilterNote columns={view.columns} filter={filter} />
        </div>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          <LanesSwitch
            level={level}
            laneCount={view.lanes.length}
            value={lanes.value}
            onChoose={lanes.choose}
          />
          <CardsSwitch compact={compact} onCompact={onCompact} />
          <FilterToggle
            filter={filter}
            open={filterOpen}
            onToggle={() => setFilterOpen(!filterOpen)}
          />
          <SettingsOpener />
        </div>
      </div>
      {filterOpen ? (
        <BoardFilterBar projectId={projectId} level={level} filter={filter} onChange={onFilter} />
      ) : null}
    </>
  );
}
