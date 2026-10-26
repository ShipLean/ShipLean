import { importTestIds as ids } from '../testIds';
import { lastStep } from './stepModel';

export function StepFooter({
  step,
  hint,
  canNext,
  nextLabel,
  onBack,
  onNext,
}: {
  step: number;
  hint: string;
  canNext: boolean;
  nextLabel: string;
  onBack: () => void;
  onNext: () => void;
}) {
  return (
    <footer className="flex items-center gap-[var(--space-3)] border-t border-edge pt-[var(--space-3)]">
      <button
        type="button"
        data-testid={ids.back}
        className="btn-ghost"
        disabled={step === 1}
        onClick={onBack}
      >
        Back
      </button>
      <span data-testid={ids.hint} className="flex-1 text-[length:var(--text-sm)] text-ink-muted">
        {hint}
      </span>
      {step < lastStep ? (
        <button
          type="button"
          data-testid={ids.next}
          className="btn-signal"
          disabled={!canNext}
          onClick={onNext}
        >
          Next: {nextLabel}
        </button>
      ) : null}
    </footer>
  );
}
