import { attachmentTestIds as ids } from '../testIds';

export type AttachmentView = 'rendered' | 'source' | 'edit';

const options = [
  { view: 'rendered', label: 'Rendered', testId: ids.rendered },
  { view: 'source', label: 'Source', testId: ids.source },
  { view: 'edit', label: 'Edit', testId: ids.edit },
] as const;

// Rendered / Source / Edit, a three-way switch.
export function ViewToggle({
  view,
  onChange,
}: {
  view: AttachmentView;
  onChange: (view: AttachmentView) => void;
}) {
  return (
    <div role="group" aria-label="View" className="inline-flex">
      {options.map((option) => (
        <button
          key={option.view}
          type="button"
          data-testid={option.testId}
          aria-pressed={view === option.view}
          className="h-7 border border-edge bg-panel px-3 text-[length:var(--text-sm)] first:rounded-l-control last:rounded-r-control aria-pressed:bg-signal-soft aria-pressed:text-signal"
          onClick={() => onChange(option.view)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
