import { componentTestIds } from './testIds';

/**
 * Committed against capacity. Over capacity turns the bar to the danger tone and fills it; it never blocks anything. The `velocity` variant is the muted bar for a
 * sprint with no capacity entries (`capacity` is then the velocity).
 */
export function CapacityBar({
  used,
  capacity,
  variant = 'capacity',
}: {
  used: number;
  capacity: number;
  variant?: 'capacity' | 'velocity';
}) {
  const over = used > capacity;
  const tone = over ? 'over' : variant === 'velocity' ? 'velocity' : 'normal';
  const percent = capacity > 0 ? Math.min(100, (used / capacity) * 100) : used > 0 ? 100 : 0;
  const fill =
    tone === 'over'
      ? 'var(--color-danger)'
      : tone === 'velocity'
        ? 'var(--color-muted)'
        : 'var(--color-accent)';
  return (
    <div
      data-testid={componentTestIds.capacityBar}
      data-tone={tone}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={capacity}
      aria-valuenow={Math.min(used, capacity)}
      className="relative h-1.5 overflow-hidden rounded-[3px] bg-sunken"
    >
      <i
        data-testid={componentTestIds.capacityBarFill}
        className="absolute inset-y-0 left-0 rounded-[3px]"
        style={{ width: `${percent}%`, background: fill }}
      />
    </div>
  );
}
