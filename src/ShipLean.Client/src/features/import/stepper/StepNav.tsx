import { importTestIds as ids } from '../testIds';
import { importSteps } from './stepModel';

const stepIds = [ids.step1, ids.step2, ids.step3, ids.step4, ids.step5];

// The five steps across the top, each with a one-line summary; a step you have passed can be clicked.
export function StepNav({
  step,
  reachable,
  summaries,
  onGo,
}: {
  step: number;
  // The furthest step the answers so far allow.
  reachable: number;
  summaries: string[];
  onGo: (step: number) => void;
}) {
  return (
    <ol aria-label="Import steps" className="m-0 flex list-none gap-[var(--space-2)] p-0">
      {importSteps.map((definition, index) => {
        const number = index + 1;
        const current = number === step;
        return (
          <li key={definition.label} className="flex-1">
            <button
              type="button"
              data-testid={stepIds[index]}
              aria-current={current ? 'step' : undefined}
              disabled={number > reachable}
              className={`flex w-full flex-col rounded-control border border-edge px-3 py-2 text-left ${
                current ? 'bg-signal-soft' : 'bg-panel'
              }`}
              onClick={() => onGo(number)}
            >
              <span className="font-medium">
                {number} · {definition.label}
              </span>
              <span className="truncate text-[length:var(--text-xs)] text-ink-muted">
                {summaries[index] ?? ''}
              </span>
            </button>
          </li>
        );
      })}
    </ol>
  );
}
