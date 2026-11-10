import type { RetroPointDto } from '@shiplean/core/api/retros';
import { PointPickRow } from './PointPickRow';

/** One labelled run of points in the picker. */
export function PointGroup({
  label,
  points,
  checked,
  onToggle,
}: {
  label: string;
  points: RetroPointDto[];
  checked: Set<string>;
  onToggle: (pointId: string) => void;
}) {
  if (points.length === 0) return null;
  return (
    <>
      <div className="px-1 pt-1.5 pb-0.5 text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {label}
      </div>
      {points.map((point) => (
        <PointPickRow
          key={point.id}
          point={point}
          checked={checked.has(point.id)}
          onToggle={() => onToggle(point.id)}
        />
      ))}
    </>
  );
}
