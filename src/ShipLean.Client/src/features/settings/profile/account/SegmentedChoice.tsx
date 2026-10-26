/** A row of exclusive buttons; the chosen one is `aria-pressed`. */
export function SegmentedChoice<T extends string>({
  label,
  testId,
  options,
  selected,
  onPick,
}: {
  label: string;
  testId: string;
  options: { value: T; label: string }[];
  selected: T;
  onPick(value: T): void;
}) {
  return (
    <div
      role="group"
      aria-label={label}
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          data-testid={testId}
          data-value={option.value}
          aria-pressed={option.value === selected}
          className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold"
          onClick={() => onPick(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
