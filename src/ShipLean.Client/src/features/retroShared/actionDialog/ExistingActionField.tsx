import type { RetroActionRefDto } from '../../../api/retros';
import { retroSharedTestIds as ids } from '../testIds';

/** Add to existing: the open action items, one chosen. */
export function ExistingActionField({
  actions,
  value,
  onChange,
}: {
  actions: RetroActionRefDto[];
  value: string;
  onChange: (workItemId: string) => void;
}) {
  return (
    <label className="grid gap-1">
      <span className="text-[length:var(--text-xs)] font-semibold text-ink-muted">Action item</span>
      <select
        data-testid={ids.retroActionExisting}
        value={value}
        className="w-full rounded-control border border-edge bg-canvas px-2 py-1 text-[length:var(--text-sm)] text-ink"
        onChange={(e) => onChange(e.target.value)}
      >
        {actions.length === 0 ? <option value="">No open action items</option> : null}
        {actions.map((action) => (
          <option key={action.workItemId} value={action.workItemId}>
            {action.key} · {action.title}
          </option>
        ))}
      </select>
    </label>
  );
}
