import { useSearchParams } from 'react-router-dom';
import type { UserRef } from '../../../api/refs';
import type { TaskboardGrouping } from '../taskboardApi';
import { taskboardTestIds as ids } from '../testIds';
import { FaceFilter } from './FaceFilter';
import { GroupBySwitch } from './GroupBySwitch';

/** Group by, the face filter, Settings (opens the settings dialog on its Fields pane) and "+ Add story or bug". */
export function TaskboardToolbar({
  grouping,
  onGroupingChange,
  people,
  faces,
  onFaceToggle,
  onAddStory,
}: {
  grouping: TaskboardGrouping;
  onGroupingChange: (next: TaskboardGrouping) => void;
  people: UserRef[];
  faces: ReadonlySet<string>;
  onFaceToggle: (faceId: string) => void;
  onAddStory: () => void;
}) {
  const [params, setParams] = useSearchParams();
  const openSettings = () => {
    const next = new URLSearchParams(params);
    next.set('settings', 'fields');
    setParams(next);
  };
  return (
    <div
      data-testid={ids.toolbar}
      className="flex items-center gap-3 border-b border-edge px-4 py-2"
    >
      <span className="flex-1" />
      <span className="text-[length:var(--text-xs)] text-ink-muted">Group by</span>
      <GroupBySwitch value={grouping} onChange={onGroupingChange} />
      <FaceFilter people={people} chosen={faces} onToggle={onFaceToggle} />
      <button type="button" data-testid={ids.settingsOpen} className="btn" onClick={openSettings}>
        ⚙ Settings
      </button>
      <button type="button" data-testid={ids.addStory} className="btn-signal" onClick={onAddStory}>
        + Add story or bug
      </button>
    </div>
  );
}
