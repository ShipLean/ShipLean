import { backlogTestIds as ids } from '../testIds';

// The count badge a dragged selection carries, drawn in the drag overlay.
export function DragBadge({ count }: { count: number }) {
  return (
    <span
      data-testid={ids.dragBadge}
      className="inline-flex h-7 items-center gap-1.5 rounded-full bg-accent px-2.5 text-[length:var(--text-sm)] font-semibold whitespace-nowrap text-accent-fg shadow-md"
    >
      {count} items
    </span>
  );
}
