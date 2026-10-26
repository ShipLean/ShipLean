import { taskboardTestIds as ids } from '../testIds';

/** The chevron that collapses or expands a row. */
export function RowToggle({
  collapsed,
  label,
  onToggle,
}: {
  collapsed: boolean;
  label: string;
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={ids.rowToggle}
      aria-expanded={!collapsed}
      aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${label}`}
      className="text-ink-muted hover:text-ink"
      onClick={onToggle}
    >
      {collapsed ? '▶' : '▼'}
    </button>
  );
}
