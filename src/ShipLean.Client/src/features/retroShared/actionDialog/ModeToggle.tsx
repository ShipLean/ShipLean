import { retroSharedTestIds as ids } from '../testIds';

export type ActionMode = 'new' | 'existing';

const modes: { id: ActionMode; label: string; testId: string }[] = [
  { id: 'new', label: 'New', testId: ids.retroActionModeNew },
  { id: 'existing', label: 'Add to existing', testId: ids.retroActionModeExisting },
];

/** The segmented New / Add to existing switch: in the dialog's header, or full width under it at 900px and below. */
export function ModeToggle({
  mode,
  onChange,
}: {
  mode: ActionMode;
  onChange: (mode: ActionMode) => void;
}) {
  return (
    <div
      role="group"
      aria-label="New or existing"
      className="flex overflow-hidden rounded-control border border-edge touch:w-full touch:overflow-visible"
    >
      {modes.map((m) => (
        <button
          key={m.id}
          type="button"
          data-testid={m.testId}
          aria-pressed={mode === m.id}
          className={`cursor-pointer px-3 py-1 text-[length:var(--text-sm)] touch:min-h-9 touch:flex-1 touch:first:rounded-l-control touch:last:rounded-r-control touch:text-[length:var(--text-base)] ${mode === m.id ? 'bg-signal-soft font-medium text-ink' : 'bg-panel text-ink-muted'}`}
          onClick={() => onChange(m.id)}
        >
          {m.label}
        </button>
      ))}
    </div>
  );
}
