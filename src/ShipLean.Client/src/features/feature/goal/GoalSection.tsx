import { useState } from 'react';
import { textareaClass } from '../../workItem/form/inputClasses';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';

// The Feature's goal, one or two sentences above the description. It edits the form's draft; Save sends it.
export function GoalSection({ draft, changed, setField, saveError }: FeatureRegionProps) {
  const [editing, setEditing] = useState(false);
  const error = saveError?.field === 'goal' ? saveError.message : null;
  return (
    <section data-testid={ids.goal} aria-label="Goal" className="grid gap-1">
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          Goal
        </h2>
        {changed.includes('goal') ? (
          <span
            role="img"
            aria-label="Unsaved change"
            className="size-1.5 rounded-full bg-signal"
          />
        ) : null}
        {editing || error ? null : (
          <button
            type="button"
            data-testid={ids.goalEdit}
            className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
            onClick={() => setEditing(true)}
          >
            Edit
          </button>
        )}
      </header>
      {editing || error ? (
        <textarea
          data-testid={ids.goalInput}
          aria-label="Goal"
          aria-invalid={error !== null}
          rows={2}
          className={`${textareaClass} min-h-0 font-[family-name:var(--font-sans)]`}
          value={draft.goal}
          onChange={(event) => setField({ goal: event.target.value })}
        />
      ) : (
        <p data-testid={ids.goalView} className="m-0 text-[length:var(--text-base)] text-ink">
          {draft.goal === '' ? (
            <span className="text-ink-muted">No goal yet. Edit to add one.</span>
          ) : (
            draft.goal
          )}
        </p>
      )}
      {error ? (
        <p
          role="alert"
          data-testid={ids.goalError}
          className="m-0 text-[length:var(--text-sm)] text-danger"
        >
          {error}
        </p>
      ) : null}
    </section>
  );
}
