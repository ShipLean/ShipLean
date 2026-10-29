import { useState } from 'react';
import type { BoardSettingsJson, SwimlaneJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { blankLane, moveLane, replaceLane } from './swimlanes/laneEdits';
import { LaneRow } from './swimlanes/LaneRow';
import { useLanePickLists } from './swimlanes/useLanePickLists';

const DEFAULT_REFUSAL =
  "The default lane can't be deleted: it takes every card no other lane takes. Rename it or move it instead.";

/** Stories and Features board lanes: checked from the top, the first criteria match wins; a card that matches none goes to the default lane. */
export function SwimlanesPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const lists = useLanePickLists(context.projectId, context.states);
  const [refusedId, setRefusedId] = useState<string>();
  const [dragFrom, setDragFrom] = useState<number>();
  const setLanes = (swimlanes: SwimlaneJson[]) => onChange({ ...draft, swimlanes });

  if (context.level === 'epics') {
    return (
      <div data-testid={ids.swimlanesPane}>
        <p data-testid={ids.lanesNotStored} className="m-0 text-ink-muted">
          The Epics board has no stored lanes. It always shows one lane.
        </p>
      </div>
    );
  }

  const remove = (lane: SwimlaneJson) => {
    if (lane.kind === 'default') {
      setRefusedId(lane.id);
      return;
    }
    setLanes(draft.swimlanes.filter((l) => l.id !== lane.id));
  };
  return (
    <div data-testid={ids.swimlanesPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Lanes split the board into rows. Criteria lanes are checked from the top and the first match
        wins. A card that matches none stays in the manual lane it was dragged to, or goes to the
        default lane. Drag the grip to reorder.
      </p>
      <div>
        <button
          type="button"
          data-testid={ids.lanesAdd}
          disabled={readOnly}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink disabled:opacity-50"
          onClick={() => setLanes([...draft.swimlanes, blankLane()])}
        >
          + Add lane
        </button>
      </div>
      {draft.swimlanes.map((lane, index) => (
        <div key={lane.id} className="grid gap-1.5">
          <LaneRow
            lane={lane}
            lists={lists}
            readOnly={readOnly}
            onChange={(next) => setLanes(replaceLane(draft.swimlanes, next))}
            onDelete={() => remove(lane)}
            onMove={(step) => setLanes(moveLane(draft.swimlanes, index, index + step))}
            onDragStart={() => setDragFrom(index)}
            onDrop={() => {
              if (dragFrom !== undefined) setLanes(moveLane(draft.swimlanes, dragFrom, index));
              setDragFrom(undefined);
            }}
          />
          {refusedId === lane.id ? (
            <p
              role="alert"
              data-testid={ids.laneRefusal}
              className="m-0 text-[length:var(--text-xs)] text-caution"
            >
              {DEFAULT_REFUSAL}
            </p>
          ) : null}
        </div>
      ))}
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Deleting a lane moves its cards to the default lane.
      </p>
    </div>
  );
}
