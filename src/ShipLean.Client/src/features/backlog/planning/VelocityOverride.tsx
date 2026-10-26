import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { VelocityDto } from '../../../api/sprints';
import { backlogTestIds as ids } from '../testIds';
import { useVelocityOverride } from './planningApi';

// The Backlog header's forecast line: the velocity it plans at (the override, else the average of the last sprints) and
// the Override button, which any member may use.
export function VelocityOverride({
  velocity,
  projectId,
  projectKey,
}: {
  velocity: VelocityDto;
  projectId: string;
  projectKey: string;
}) {
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState('');
  const save = useVelocityOverride(projectId, projectKey);
  const planned = velocity.override ?? velocity.average;
  const recent = velocity.sprints.map((s) => s.completed).join(' · ');

  function open() {
    setText(String(planned));
    save.reset();
    setEditing(true);
  }
  function submit(value: number | null) {
    save.mutate(value, { onSuccess: () => setEditing(false) });
  }
  const parsed = Number(text);
  const valid = text.trim() !== '' && Number.isFinite(parsed) && parsed >= 0;
  return (
    <span className="inline-flex items-center gap-2 text-[length:var(--text-sm)]">
      <span data-testid={ids.planningVelocity} data-overridden={velocity.override !== null}>
        Forecast at velocity <b>{planned}</b>{' '}
        <span className="text-ink-muted">
          {velocity.override !== null
            ? '(override)'
            : `(average of last ${velocity.sprints.length} sprints${recent === '' ? '' : `: ${recent}`})`}
        </span>
      </span>
      {editing ? (
        <>
          <input
            type="number"
            min={0}
            aria-label="Velocity override"
            data-testid={ids.planningOverrideInput}
            className="field w-20"
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
          <button
            type="button"
            data-testid={ids.planningOverrideSave}
            className="btn-signal"
            disabled={!valid || save.isPending}
            onClick={() => submit(parsed)}
          >
            Save
          </button>
          {velocity.override !== null ? (
            <button
              type="button"
              data-testid={ids.planningOverrideClear}
              className="btn-ghost"
              disabled={save.isPending}
              onClick={() => submit(null)}
            >
              Use average
            </button>
          ) : null}
          <button
            type="button"
            data-testid={ids.planningOverrideCancel}
            className="btn-ghost"
            onClick={() => setEditing(false)}
          >
            Cancel
          </button>
          {save.error ? (
            <span data-testid={ids.planningOverrideError} role="alert" className="text-alert">
              {save.error instanceof ApiRequestError ? save.error.message : 'Could not save'}
            </span>
          ) : null}
        </>
      ) : (
        <button
          type="button"
          data-testid={ids.planningOverrideButton}
          className="btn-ghost"
          onClick={open}
        >
          Override
        </button>
      )}
    </span>
  );
}
