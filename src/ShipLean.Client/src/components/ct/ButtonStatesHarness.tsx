import { buttonHarnessIds as ids } from './buttonHarnessIds';

export type ButtonClass = 'btn-signal' | 'btn-ghost' | 'btn-danger-ghost';

/** CT-only: one shared button class, enabled beside disabled, on the page background. */
export function ButtonStatesHarness({ variant }: { variant: ButtonClass }) {
  return (
    <div data-testid={ids.ctButtonStates} className="inline-flex gap-3 bg-canvas p-4 text-ink">
      <button type="button" data-testid={ids.ctButtonEnabled} className={variant}>
        Save
      </button>
      <button type="button" data-testid={ids.ctButtonDisabled} className={variant} disabled>
        Save
      </button>
    </div>
  );
}
