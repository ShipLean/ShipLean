import { backlogTestIds as ids } from '../testIds';

// The arrow that opens or closes a section. Off while a search is on: the search decides which sections open.
export function SectionToggle({
  title,
  open,
  disabled,
  onToggle,
}: {
  title: string;
  open: boolean;
  disabled: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={ids.sectionToggle}
      className="btn-ghost px-1 disabled:opacity-50 touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)]"
      aria-expanded={open}
      aria-label={`${open ? 'Collapse' : 'Expand'} ${title}`}
      disabled={disabled}
      onClick={onToggle}
    >
      {open ? '▼' : '▶'}
    </button>
  );
}
