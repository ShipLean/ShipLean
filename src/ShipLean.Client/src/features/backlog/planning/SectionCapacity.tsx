import { CapacityBar } from '../../../components/CapacityBar';
import { Chip } from '../../../components/Chip';
import type { SprintSum } from '../bulk/sprintSum';
import { backlogTestIds as ids } from '../testIds';
import { capacityLoad } from './capacityLoad';

// A sprint section's load: committed against the team's capacity, with the points free or over (a warning, never a
// block). With no capacity entered the muted bar is drawn against the velocity the forecast plans at, beside a
// velocity chip. Without a velocity either there is only the committed points and the chip.
function Capacity({
  committed,
  capacity,
  velocity,
}: {
  committed: number;
  capacity: number | null;
  velocity: number | null;
}) {
  const { limit, fromVelocity, over, free } = capacityLoad(committed, capacity, velocity);
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
          variant={fromVelocity ? 'velocity' : 'capacity'}
        />
      </span>
      <span data-testid={ids.planningCommitted}>
        <b>{committed}</b> / {limit} pts
      </span>
      {fromVelocity ? chip : null}
      {over === null ? (
        <span data-testid={ids.planningFree} className="text-ink-muted">
          {free} free
        </span>
      ) : (
        <span data-testid={ids.planningOver} className="font-semibold text-alert">
          +{over} over
        </span>
      )}
    </span>
  );
}

// A section's load, led by what it would hold while a dragged selection is over it.
export function SectionCapacity({
  preview = null,
  ...load
}: {
  committed: number;
  capacity: number | null;
  velocity: number | null;
  preview?: SprintSum | null;
}) {
  return (
    <span className="inline-flex items-center gap-3">
      {preview === null ? null : (
        <span
          data-testid={ids.sectionPreview}
          data-over={preview.over}
          className={`text-[length:var(--text-xs)] font-semibold whitespace-nowrap ${preview.over ? 'text-alert' : 'text-accent'}`}
        >
          {preview.text}
        </span>
      )}
      <Capacity {...load} />
    </span>
  );
}
