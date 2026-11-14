import { useState } from 'react';
import type { BoardViewDto } from '@shiplean/core/api/views';
import type { Level } from '../../../routes';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { BoardFilterQuery } from '../boardApi';
import { BoardFilterBar } from '../filter/BoardFilterBar';
import { BoardFilterSheet } from '../filter/BoardFilterSheet';
import { FilterNote } from '../filter/FilterNote';
import { FilterToggle } from '../filter/FilterToggle';
import { filterCount, hiddenCards } from '../filter/filterQuery';
import { useBoardLanes } from '../lanes/boardPreferences';
import { featuresLaneChoice, LanesSwitch } from '../lanes/LanesSwitch';
import { boardTestIds as ids } from '../testIds';
import { BoardMoreSheet } from '../toolbar/BoardMoreSheet';
import { CardsSwitch } from '../toolbar/CardsSwitch';
import { LevelSwitch } from '../toolbar/LevelSwitch';
import { SettingsOpener } from '../toolbar/SettingsOpener';

// The lanes the meta line names: Swimlanes (Stories, and Features' stored lanes) or By Epic; none for one lane.
function lanesLabel(level: Level, value: string | null, laneCount: number): string | null {
  if (level === 'epics') return null;
  if (level === 'features') {
    const choice = featuresLaneChoice(value);
    if (choice === 'epic') return 'By Epic';
    return choice === 'on' && laneCount > 1 ? 'Swimlanes' : null;
  }
  return value !== 'off' && laneCount > 1 ? 'Swimlanes' : null;
}

// The rows over the grid: the level switch, what is open, the lane and Full / Compact choices, Filter and Board
// settings; under them the filter bar while it is open. At 900px and below it is one row (level switch, Filter, ⋯ More)
// with the summary on a meta line under it, and Filter opens a sheet.
export function BoardToolbar({
  view,
  projectId,
  boardId,
  level,
  compact,
  onCompact,
  filter,
  onFilter,
}: {
  view: BoardViewDto;
  projectId: string;
  boardId: string;
  level: Level;
  compact: boolean;
  onCompact: (compact: boolean) => void;
  filter: BoardFilterQuery;
  onFilter: (filter: BoardFilterQuery) => void;
}) {
  const [filterOpen, setFilterOpen] = useState(false);
  const lanes = useBoardLanes(projectId, boardId);
  const { touch } = useTouchShell();
  if (touch) {
    const total = view.columns.reduce((sum, column) => sum + column.count, 0);
    const shown = total - hiddenCards(view.columns);
    const laneText = lanesLabel(level, lanes.value, view.lanes.length);
    const parts = [
      <span key="summary" data-testid={ids.summary} className="tabular-nums">
        <b className="font-semibold text-ink">{view.openCount}</b> open ·{' '}
        <b className="font-semibold text-ink">{view.openPoints}</b> pts
      </span>,
      laneText === null ? null : <span key="lanes">{laneText}</span>,
      compact ? <span key="cards">Compact</span> : null,
      filterCount(filter) === 0 ? null : (
        <span key="filtered" className="text-signal">
          Filtered: {shown} of {total} cards
        </span>
      ),
    ].filter((part) => part !== null);
    return (
      <>
        <div data-testid={ids.toolbar} className="flex-none">
          <div className="flex flex-nowrap items-center gap-2 pt-2">
            <LevelSwitch level={level} />
            <FilterToggle
              filter={filter}
              open={filterOpen}
              onToggle={() => setFilterOpen(!filterOpen)}
            />
            <span className="flex-1" />
            <BoardMoreSheet
              level={level}
              laneCount={view.lanes.length}
              lanesValue={lanes.value}
              onChooseLanes={lanes.choose}
              compact={compact}
              onCompact={onCompact}
            />
          </div>
          <div
            data-testid={ids.meta}
            className="flex flex-wrap items-center gap-x-1.5 gap-y-0.5 pt-1.5 pb-2 text-[length:var(--text-xs)] text-ink-muted"
          >
            {parts.map((part, index) => (
              <span key={part.key} className="inline-flex items-center gap-1.5">
                {index === 0 ? null : <span aria-hidden="true">·</span>}
                {part}
              </span>
            ))}
          </div>
        </div>
        <BoardFilterSheet
          open={filterOpen}
          projectId={projectId}
          level={level}
          filter={filter}
          onChange={onFilter}
          onClose={() => setFilterOpen(false)}
        />
      </>
    );
  }
  return (
    <>
      <div
        data-testid={ids.toolbar}
        className="flex flex-none flex-wrap items-center gap-2 border-b border-edge py-2 text-[length:var(--text-sm)]"
      >
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
