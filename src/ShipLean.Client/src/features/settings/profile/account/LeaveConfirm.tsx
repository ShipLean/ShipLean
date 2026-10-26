import { useEffect, useRef } from 'react';
import type { ProjectSummaryDto } from '../../../../api/projects';
import { settingsTestIds } from '../../testIds';

/** The question under a project's row. A refusal shows the server's own message. */
export function LeaveConfirm({
  project,
  error,
  leaving,
  onConfirm,
  onCancel,
}: {
  project: ProjectSummaryDto;
  error: string | null;
  leaving: boolean;
  onConfirm(): void;
  onCancel(): void;
}) {
  const cancel = useRef<HTMLButtonElement>(null);
  // The Leave button that opened this question is disabled, so keyboard focus moves here.
  useEffect(() => cancel.current?.focus(), []);
  return (
    <li className="flex flex-wrap items-center gap-2 border-b border-edge/60 bg-canvas px-3 py-2 text-[length:var(--text-sm)]">
      <span>
        Leave{' '}
        <b>
          {project.key} · {project.name}
        </b>
        ? You lose access straight away. Items assigned to you stay assigned.
      </span>
      {error === null ? null : (
        <span data-testid={settingsTestIds.myProjectLeaveError} role="alert" className="text-alert">
          {error}
        </span>
      )}
      <span className="flex-1" />
      <button
        ref={cancel}
        type="button"
        data-testid={settingsTestIds.myProjectLeaveCancel}
        className="btn-ghost"
        onClick={onCancel}
      >
        Cancel
      </button>
      <button
        type="button"
        data-testid={settingsTestIds.myProjectLeaveConfirm}
        className="btn-signal disabled:opacity-50"
        disabled={leaving}
        onClick={onConfirm}
      >
        Leave project
      </button>
    </li>
  );
}
