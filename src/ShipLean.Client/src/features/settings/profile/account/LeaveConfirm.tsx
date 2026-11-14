import { useEffect, useRef } from 'react';
import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
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
    <li
      data-testid={settingsTestIds.myProjectLeaveRow}
      className="flex flex-wrap items-center gap-2 border-b border-edge/60 bg-canvas px-3 py-2 text-[length:var(--text-sm)] touch:p-3"
    >
      <span className="touch:basis-full">
        Leave{' '}
        <b>
          {project.key} · {project.name}
        </b>
        ? You lose access straight away. Items assigned to you stay assigned.
      </span>
      {error === null ? null : (
        <span
          data-testid={settingsTestIds.myProjectLeaveError}
          role="alert"
          className="text-alert touch:basis-full"
        >
          {error}
        </span>
      )}
      <span className="flex-1 touch:hidden" />
      <button
        ref={cancel}
        type="button"
        data-testid={settingsTestIds.myProjectLeaveCancel}
        className="btn-ghost touch:h-[var(--touch-target)] touch:flex-1"
        onClick={onCancel}
      >
        Cancel
      </button>
      <button
        type="button"
        data-testid={settingsTestIds.myProjectLeaveConfirm}
        className="btn-signal disabled:opacity-50 touch:border touch:border-transparent touch:h-[var(--touch-target)] touch:flex-1"
        disabled={leaving}
        onClick={onConfirm}
      >
        Leave project
      </button>
    </li>
  );
}
