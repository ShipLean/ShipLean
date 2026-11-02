import type { RetroCategory, RetroPointDto } from '../../../api/retros';
import { PointGroup } from './PointGroup';

const offeredGroups: { category: RetroCategory; label: string }[] = [
  { category: 'improve', label: 'Can be improved, no action item yet' },
  { category: 'wentBadly', label: 'Went badly, no action item yet' },
  { category: 'notInPlan', label: 'Not in the plan, no action item yet' },
];

/** Points it covers: Selected first, then what else is undecided, by category. The list scrolls inside the dialog. */
export function PointPicker({
  selected,
  offered,
  checked,
  onToggle,
}: {
  selected: RetroPointDto[];
  offered: RetroPointDto[];
  checked: Set<string>;
  onToggle: (pointId: string) => void;
}) {
  const count = [...selected, ...offered].filter((point) => checked.has(point.id)).length;
  const empty = selected.length === 0 && offered.length === 0;
  return (
    <div className="grid gap-1">
      <span className="text-[length:var(--text-xs)] font-semibold text-ink-muted">
        Points it covers <span className="font-normal">· {count} selected</span>
      </span>
      {empty ? (
        <p className="m-0 rounded-control border border-edge bg-canvas p-3 text-[length:var(--text-sm)] text-ink-muted">
          Every point is decided already, so there is nothing to cover.
        </p>
      ) : (
        <div className="grid max-h-[260px] gap-0.5 overflow-auto rounded-control border border-edge bg-canvas p-1">
          <PointGroup label="Selected" points={selected} checked={checked} onToggle={onToggle} />
          {offeredGroups.map((group) => (
            <PointGroup
              key={group.category}
              label={group.label}
              points={offered.filter((point) => point.category === group.category)}
              checked={checked}
              onToggle={onToggle}
            />
          ))}
        </div>
      )}
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        Quoted on the action item with a link back. Each one then counts as decided.
      </span>
    </div>
  );
}
