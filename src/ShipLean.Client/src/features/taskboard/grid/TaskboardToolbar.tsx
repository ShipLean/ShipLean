import { useState } from 'react';
import type { UserRef } from '@shiplean/core/api/refs';
import type { SheetAction } from '../../../touch/ActionSheet';
import { MoreButton } from '../../../touch/MoreButton';
import type { TaskboardGrouping } from '../taskboardApi';
import { taskboardTestIds as ids } from '../testIds';
import { FaceFilter } from './FaceFilter';
import { FilterSheet } from './FilterSheet';
import { GroupBySwitch } from './GroupBySwitch';
import { useOpenCardSettings } from './useOpenCardSettings';

/**
 * Group by, the face filter, Settings (opens the settings dialog on its Fields pane) and "+ Add story or bug". At 900px
 * and below (SHIP-722 D5) it is one row: Stories | People, Filter (n) (a sheet), ⋯ (the slot's `more` actions, which
 * hold Settings there) and + Add.
 */
export function TaskboardToolbar({
  grouping,
  onGroupingChange,
  people,
  faces,
  faceCounts,
  onFaceToggle,
  onFacesClear,
  more,
  onAddStory,
}: {
  grouping: TaskboardGrouping;
  onGroupingChange: (next: TaskboardGrouping) => void;
  people: UserRef[];
  faces: ReadonlySet<string>;
  /** Each face's task count on this board, for the Filter sheet. */
  faceCounts: ReadonlyMap<string, number>;
  onFaceToggle: (faceId: string) => void;
  onFacesClear: () => void;
  /** The ⋯ sheet's actions at 900px and below; each board passes its own. */
  more: readonly SheetAction[];
  onAddStory: () => void;
}) {
  const [filterOpen, setFilterOpen] = useState(false);
  const openSettings = useOpenCardSettings();
  return (
    <div
      data-testid={ids.toolbar}
      className="flex items-center gap-3 border-b border-edge px-4 py-2 touch:gap-2"
    >
      <span className="flex-1 touch:hidden" />
      <span className="text-[length:var(--text-xs)] text-ink-muted touch:hidden">Group by</span>
      <GroupBySwitch value={grouping} onChange={onGroupingChange} />
      <span className="hidden flex-1 touch:block" />
      <FaceFilter people={people} chosen={faces} onToggle={onFaceToggle} className="touch:hidden" />
      <button
        type="button"
        data-testid={ids.filterButton}
        className="btn hidden touch:inline-flex touch:h-[var(--touch-target)] touch:min-w-[var(--touch-target)]"
        onClick={() => setFilterOpen(true)}
      >
        Filter{faces.size > 0 ? ` (${faces.size})` : ''}
      </button>
      <button
        type="button"
        data-testid={ids.settingsOpen}
        className="btn touch:hidden"
        onClick={openSettings}
      >
        ⚙ Settings
      </button>
      <span className="hidden touch:contents">
        <MoreButton actions={more} label="More" />
      </span>
      <button
        type="button"
        data-testid={ids.addStory}
        className="btn-signal touch:h-[var(--touch-target)] touch:min-w-[var(--touch-target)]"
        onClick={onAddStory}
      >
        <span className="touch:hidden">+ Add story or bug</span>
        <span className="hidden touch:inline">+ Add</span>
      </button>
      <FilterSheet
        open={filterOpen}
        onClose={() => setFilterOpen(false)}
        people={people}
        chosen={faces}
        counts={faceCounts}
        onToggle={onFaceToggle}
        onClear={onFacesClear}
      />
    </div>
  );
}
