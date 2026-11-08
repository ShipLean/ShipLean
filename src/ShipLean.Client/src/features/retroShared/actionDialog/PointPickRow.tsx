import type { RetroPointDto } from '../../../api/retros';
import { retroCategoryLabels } from '../retroCategories';
import { retroSharedTestIds as ids } from '../testIds';

/** One point the action item can cover: a checkbox, the text, and its category, item and theme beneath. */
export function PointPickRow({
  point,
  checked,
  onToggle,
}: {
  point: RetroPointDto;
  checked: boolean;
  onToggle: () => void;
}) {
  return (
    <label
      data-testid={ids.retroActionPick}
      className={`grid cursor-pointer grid-cols-[auto_minmax(0,1fr)] gap-1.5 rounded-[var(--radius-xs)] p-1 text-[length:var(--text-sm)] text-ink touch:min-h-[var(--touch-target)] touch:py-1.5 touch:text-[length:var(--text-base)] ${checked ? 'bg-signal-soft' : 'hover:bg-sunken'}`}
    >
      <input
        type="checkbox"
        data-testid={ids.retroActionPickCheck}
        checked={checked}
        className="mt-0.5 accent-[var(--color-accent)]"
        onChange={onToggle}
      />
      <span>
        {point.text}
        <small className="block text-[length:var(--text-xs)] text-ink-muted">
          {retroCategoryLabels[point.category]}
          {point.workItem
            ? ` · ${point.workItem.key}`
            : point.sprint
              ? ` · ${point.sprint.name}`
              : ''}
          {` · # ${point.theme?.name ?? 'Unthemed'}`}
        </small>
      </span>
    </label>
  );
}
