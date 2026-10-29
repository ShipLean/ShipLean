import type { ReactNode } from 'react';
import type { TransitionJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { actionSummary, transitionLabel } from './transitionEdits';

/**
 * One transition: what it is, the Invalid badge and reason, a summary of its actions, delete. `actions` is where the
 * action editor goes (below the summary), so the row's own layout doesn't change when it lands.
 */
export function TransitionRow({
  transition,
  invalid,
  readOnly,
  actions,
  onDelete,
}: {
  transition: TransitionJson;
  /** Why it can never fire, or null. */
  invalid: string | null;
  readOnly: boolean;
  actions?: ReactNode;
  onDelete(): void;
}) {
  const label = transitionLabel(transition);
  return (
    <div
      data-testid={ids.transitionRow}
      className="grid gap-1.5 rounded-panel border border-edge bg-panel p-2.5"
    >
      <div className="flex items-center gap-2">
        <span data-testid={ids.transitionLabel} className="font-medium text-ink">
          {label}
        </span>
        {invalid === null ? null : (
          <span
            data-testid={ids.transitionInvalid}
            title={invalid}
            className="rounded-control border border-caution px-1.5 text-[length:var(--text-xs)] text-caution"
          >
            Invalid
          </span>
        )}
        <span className="flex-1" />
        <button
          type="button"
          data-testid={ids.transitionDelete}
          disabled={readOnly}
          aria-label={`Delete ${label}`}
          className="h-6 rounded-control px-2 text-[length:var(--text-sm)] text-ink-muted hover:bg-sunken disabled:cursor-default disabled:opacity-35"
          onClick={onDelete}
        >
          Delete
        </button>
      </div>
      {invalid === null ? null : (
        <p
          data-testid={ids.transitionInvalidReason}
          className="m-0 text-[length:var(--text-xs)] text-caution"
        >
          {invalid}. It never fires. Delete it, or add a state with that name back.
        </p>
      )}
      <p
        data-testid={ids.transitionSummary}
        className="m-0 truncate text-[length:var(--text-sm)] text-ink-muted"
      >
        {actionSummary(transition)}
      </p>
      {actions}
    </div>
  );
}
