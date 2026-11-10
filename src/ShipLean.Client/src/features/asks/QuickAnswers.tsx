import { asksTestIds as ids } from './testIds';

// One button per option of an open question. A click only chooses the text; the person posts it with Post answer.
export function QuickAnswers({
  label,
  options,
  onChoose,
}: {
  label: string;
  options: readonly string[];
  onChoose(option: string): void;
}) {
  if (options.length === 0) return null;
  return (
    <div role="group" aria-label={`Quick answers for ${label}`} className="flex flex-wrap gap-1.5">
      {options.map((option, index) => (
        <button
          key={`${index}-${option}`}
          type="button"
          data-testid={ids.questionQuickAnswer}
          className="min-h-7 max-w-full rounded-control border border-edge bg-sunken px-3 py-1 text-left [overflow-wrap:anywhere] text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={() => onChoose(option)}
        >
          {option}
        </button>
      ))}
    </div>
  );
}
