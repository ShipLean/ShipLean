import { CapacityBar } from '../../../components/CapacityBar';
import { Chip } from '../../../components/Chip';
import { backlogTestIds as ids } from '../testIds';

// A sprint section's load: committed against the team's capacity, with the points free or over (a warning, never a
// block). With no capacity entered the muted bar is drawn against the velocity the forecast plans at, beside a
// velocity chip. Without a velocity either there is only the committed points and the chip.
export function SectionCapacity({
  committed,
  capacity,
  velocity,
}: {
  committed: number;
  capacity: number | null;
  velocity: number | null;
}) {
  const limit = capacity ?? velocity;
  const chip = (
    <span
      data-testid={ids.planningVelocityChip}
      title="Nobody has entered capacity for this sprint yet"
    >
      <Chip tone="warn">{velocity === null ? 'velocity' : `velocity ${velocity}`}</Chip>
    </span>
  );
  if (limit === null) {
    return (
      <span className="inline-flex items-center gap-2 whitespace-nowrap text-[length:var(--text-sm)]">
        <span data-testid={ids.planningCommitted}>
          <b>{committed}</b> pts
        </span>
        {chip}
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-2 whitespace-nowrap text-[length:var(--text-sm)]">
      <span className="w-[120px]">
        <CapacityBar
          used={committed}
          capacity={limit}
          variant={capacity === null ? 'velocity' : 'capacity'}
        />
      </span>
      <span data-testid={ids.planningCommitted}>
        <b>{committed}</b> / {limit} pts
      </span>
      {capacity === null ? chip : null}
      {committed > limit ? (
        <span data-testid={ids.planningOver} className="font-semibold text-alert">
          +{committed - limit} over
        </span>
      ) : (
        <span data-testid={ids.planningFree} className="text-ink-muted">
          {limit - committed} free
        </span>
      )}
    </span>
  );
}
