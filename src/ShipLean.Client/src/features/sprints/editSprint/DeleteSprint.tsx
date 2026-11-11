import { useEffect, useRef, useState } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { useDeleteSprint } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';

/**
 * Delete sprint at the foot of the Edit sprint rail, and the question it asks first. Only a sprint with no work items can
 * go; the server says so when it has some, and its message shows here.
 */
export function DeleteSprint({
  projectId,
  sprint,
  onDeleted,
}: {
  projectId: string;
  sprint: SprintDto;
  onDeleted: () => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const remove = useDeleteSprint(projectId, sprint);
  const open = useRef<HTMLButtonElement>(null);
  const cancel = useRef<HTMLButtonElement>(null);
  const asked = useRef(false);
  useEffect(() => {
    // Focus follows the question: onto Cancel when it opens, back to Delete sprint when it closes.
    if (confirming) cancel.current?.focus();
    else if (asked.current) open.current?.focus();
    asked.current = confirming;
  }, [confirming]);

  if (!confirming) {
    return (
      <div className="flex border-t border-edge pt-2">
        <button
          ref={open}
          type="button"
          data-testid={ids.deleteSprint}
          className="btn-ghost text-alert hover:text-alert"
          onClick={() => setConfirming(true)}
        >
          Delete sprint
        </button>
      </div>
    );
  }

  const error =
    remove.error instanceof ApiRequestError
      ? remove.error.message
      : remove.error
        ? 'The sprint could not be deleted.'
        : null;
  return (
    <div className="flex flex-wrap items-center gap-2 border-t border-edge pt-2 text-[length:var(--text-sm)]">
      <span>
        Delete <b>{sprint.name}</b>? Only a sprint with no work items can be deleted.
      </span>
      {error === null ? null : (
        <span data-testid={ids.deleteSprintError} role="alert" className="text-alert">
          {error}
        </span>
      )}
      <span className="flex-1" />
      <button
        ref={cancel}
        type="button"
        data-testid={ids.deleteSprintCancel}
        className="btn-ghost"
        onClick={() => {
          remove.reset();
          setConfirming(false);
        }}
      >
        Cancel
      </button>
      <button
        type="button"
        data-testid={ids.deleteSprintConfirm}
        className="btn-signal disabled:opacity-50"
        disabled={remove.isPending}
        onClick={() => remove.mutate(undefined, { onSuccess: onDeleted })}
      >
        Delete sprint
      </button>
    </div>
  );
}
