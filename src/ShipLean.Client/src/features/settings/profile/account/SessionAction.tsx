/** An action and what it does, one row of the sessions panel. */
export function SessionAction({
  title,
  text,
  label,
  testId,
  pending,
  onClick,
}: {
  title: string;
  text: string;
  label: string;
  testId: string;
  pending: boolean;
  onClick(): void;
}) {
  return (
    <div className="flex items-center gap-3 border-b border-edge/60 px-3 py-2.5 last:border-b-0">
      <span className="min-w-0 flex-1 text-[length:var(--text-sm)]">
        <b className="block font-semibold">{title}</b>
        <span className="text-[length:var(--text-xs)] text-ink-muted">{text}</span>
      </span>
      <button
        type="button"
        data-testid={testId}
        className="btn-ghost disabled:opacity-50"
        disabled={pending}
        onClick={onClick}
      >
        {label}
      </button>
    </div>
  );
}
