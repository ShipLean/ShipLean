import { useState } from 'react';
import type { BoardTypeStatesDto } from '../../../../api/boards';
import type { TransitionJson } from '../../../../api/persisted';
import type { WorkItemTypeId } from '../../../../api/refs';
import { boardSettingsTestIds as ids } from '../../testIds';
import { liveStatesOf, newTransition, refusalFor, TRANSITION_TYPE_LABELS } from './transitionEdits';

const ANY = '';

/** Type (when the board has more than one), From (Any state or a live state) and To; refuses a duplicate inline. */
export function AddTransitionForm({
  types,
  states,
  transitions,
  readOnly,
  onAdd,
}: {
  types: WorkItemTypeId[];
  states: BoardTypeStatesDto[];
  transitions: TransitionJson[];
  readOnly: boolean;
  onAdd(next: TransitionJson): void;
}) {
  const [type, setType] = useState<WorkItemTypeId>(types[0] ?? 'story');
  const [fromId, setFromId] = useState(ANY);
  const [toId, setToId] = useState('');
  const [refusal, setRefusal] = useState<string | null>(null);
  const live = liveStatesOf(states, type);

  const add = () => {
    const to = live.find((s) => s.id === toId);
    if (to === undefined) return;
    const fromState = live.find((s) => s.id === fromId);
    const from = fromState === undefined ? null : { stateId: fromState.id, name: fromState.name };
    const toRef = { stateId: to.id, name: to.name };
    const refused = refusalFor(transitions, type, from, toRef);
    setRefusal(refused);
    if (refused !== null) return;
    onAdd(newTransition(type, from, toRef));
    setFromId(ANY);
    setToId('');
  };
  const pickType = (next: WorkItemTypeId) => {
    setType(next);
    setFromId(ANY);
    setToId('');
    setRefusal(null);
  };

  return (
    <div className="grid gap-1.5">
      <div className="flex flex-wrap items-end gap-2">
        {types.length > 1 ? (
          <label className="grid gap-0.5 text-[length:var(--text-xs)] text-ink-muted">
            Type
            <select
              data-testid={ids.transitionAddType}
              className="field h-[26px]"
              value={type}
              disabled={readOnly}
              onChange={(event) => pickType(event.target.value as WorkItemTypeId)}
            >
              {types.map((t) => (
                <option key={t} value={t}>
                  {TRANSITION_TYPE_LABELS[t] ?? t}
                </option>
              ))}
            </select>
          </label>
        ) : null}
        <label className="grid gap-0.5 text-[length:var(--text-xs)] text-ink-muted">
          From
          <select
            data-testid={ids.transitionAddFrom}
            className="field h-[26px]"
            value={fromId}
            disabled={readOnly}
            onChange={(event) => {
              setFromId(event.target.value);
              setRefusal(null);
            }}
          >
            <option value={ANY}>Any state</option>
            {live.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-0.5 text-[length:var(--text-xs)] text-ink-muted">
          To
          <select
            data-testid={ids.transitionAddTo}
            className="field h-[26px]"
            value={toId}
            disabled={readOnly}
            onChange={(event) => {
              setToId(event.target.value);
              setRefusal(null);
            }}
          >
            <option value="">Choose a state</option>
            {live.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          data-testid={ids.transitionAdd}
          disabled={readOnly || toId === ''}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink disabled:opacity-50"
          onClick={add}
        >
          + Add transition
        </button>
      </div>
      {refusal === null ? null : (
        <p
          role="alert"
          data-testid={ids.transitionAddRefusal}
          className="m-0 text-[length:var(--text-xs)] text-caution"
        >
          {refusal}
        </p>
      )}
    </div>
  );
}
