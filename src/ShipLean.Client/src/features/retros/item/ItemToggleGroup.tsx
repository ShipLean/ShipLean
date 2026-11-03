/** A row of mutually exclusive buttons in one bordered strip; the pressed one is filled. */
export function ItemToggleGroup({
  label,
  testId,
  options,
  value,
  onChange,
}: {
  label: string;
  testId: string;
  options: { id: string; label: string }[];
  value: string;
  onChange: (id: string) => void;
}) {
  return (
    <div
      role="group"
      aria-label={label}
      data-testid={testId}
      className="inline-flex overflow-hidden rounded-control border border-edge"
    >
      {options.map((option) => (
        <button
          key={option.id}
          type="button"
          data-value={option.id}
          aria-pressed={value === option.id}
          className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] whitespace-nowrap text-ink-muted last:border-r-0 aria-pressed:bg-signal-soft aria-pressed:font-semibold aria-pressed:text-ink"
          onClick={() => onChange(option.id)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
