import { SPLIT_COLOURS } from '../leadtime/SplitBar';
import { inspectionMetricsTestIds as ids } from '../testIds';

export interface PartsHours {
  active: number;
  blocked: number;
  rework: number;
}

type Kind = keyof PartsHours;

/** A lead time as a bar `lead / max` of the cell wide, split into active, blocked and rework (Lead time view's colours). */
export function PartsBar({ lead, parts, max }: { lead: number; parts: PartsHours; max: number }) {
  if (max <= 0) return null;
  const total = parts.active + parts.blocked + parts.rework;
  return (
    <span
      className="flex h-2 min-w-[2px] overflow-hidden rounded-sm"
      style={{ width: `${Math.max(2, (lead / max) * 100)}%` }}
    >
      {(Object.keys(SPLIT_COLOURS) as Kind[]).map((kind) => (
        <i
          key={kind}
          data-testid={ids.splitSegment}
          data-kind={kind}
          className="block h-full"
          style={{
            width: total > 0 ? `${(parts[kind] / total) * 100}%` : '0%',
            background: SPLIT_COLOURS[kind],
          }}
        />
      ))}
    </span>
  );
}
