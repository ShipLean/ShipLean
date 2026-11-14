import { ApiRequestError } from '@shiplean/core/api/client';
import type { VelocityDto } from '@shiplean/core/api/sprints';
import { Sheet } from '../../../../touch/Sheet';
import { backlogTestIds as ids } from '../../testIds';
import { useVelocityDraft } from '../useVelocityDraft';

function VelocityForm({
  velocity,
  projectId,
  projectKey,
  onDone,
}: {
  velocity: VelocityDto;
  projectId: string;
  projectKey: string;
  onDone: () => void;
}) {
  const { planned, text, setText, save, parsed, valid, submit } = useVelocityDraft(
    velocity,
    projectId,
    projectKey,
  );
  return (
    <div data-testid={ids.planningOverrideSheet} className="flex flex-col gap-3 px-2 pb-2">
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        The forecast plans at velocity <b className="text-ink">{planned}</b>
        {velocity.override === null ? ' (the average of the last sprints)' : ' (your override)'}.
      </p>
      <input
        type="number"
        min={0}
        aria-label="Velocity override"
        data-testid={ids.planningOverrideInput}
        className="field min-h-[var(--touch-target)]"
        value={text}
        onChange={(event) => setText(event.target.value)}
      />
      {save.error ? (
        <span data-testid={ids.planningOverrideError} role="alert" className="text-alert">
          {save.error instanceof ApiRequestError ? save.error.message : 'Could not save'}
        </span>
      ) : null}
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          data-testid={ids.planningOverrideSave}
          className="btn-signal min-h-[var(--touch-target)]"
          disabled={!valid || save.isPending}
          onClick={() => submit(parsed, onDone)}
        >
          Save
        </button>
        {velocity.override !== null ? (
          <button
            type="button"
            data-testid={ids.planningOverrideClear}
            className="btn-ghost min-h-[var(--touch-target)]"
            disabled={save.isPending}
            onClick={() => submit(null, onDone)}
          >
            Use average
          </button>
        ) : null}
      </div>
    </div>
  );
}

/** ⋯ More → Override velocity at touch: the override edited in a bottom sheet (the desktop edits it inline). */
export function TouchVelocitySheet({
  open,
  onClose,
  velocity,
  projectId,
  projectKey,
}: {
  open: boolean;
  onClose: () => void;
  velocity: VelocityDto;
  projectId: string;
  projectKey: string;
}) {
  return (
    <Sheet open={open} title="Override velocity" onClose={onClose}>
      <VelocityForm
        velocity={velocity}
        projectId={projectId}
        projectKey={projectKey}
        onDone={onClose}
      />
    </Sheet>
  );
}
