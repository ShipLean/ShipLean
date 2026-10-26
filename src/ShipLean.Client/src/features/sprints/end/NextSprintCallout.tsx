import type { SprintRef } from '../../../api/refs';
import { sprintsTestIds as ids } from '../testIds';
import type { NextSprintFigures } from './rolloverChoices';

/** The next sprint's total against its team capacity after this move: a warning when over, never a block. */
export function NextSprintCallout({
  sprint,
  figures,
}: {
  sprint: SprintRef;
  figures: NextSprintFigures;
}) {
  const over = figures.over > 0;
  return (
    <div
      data-testid={ids.endAfter}
      data-over={over}
      role={over ? 'alert' : undefined}
      className={`rounded-panel border border-edge border-l-[3px] bg-sunken px-3 py-2 text-[length:var(--text-sm)] ${
        over ? 'border-l-caution' : 'border-l-edge'
      }`}
    >
      {figures.capacity === null ? (
        <>
          <b className="block">{`${sprint.name} after this: ${figures.total} pts`}</b>
          No capacity is entered for it yet.
        </>
      ) : (
        <>
          <b className="block">{`${sprint.name} after this: ${figures.total} / ${figures.capacity} pts`}</b>
          {over
            ? `${figures.over} over team capacity. A warning only; nothing is blocked.`
            : `${figures.room} pts of room left.`}
        </>
      )}
    </div>
  );
}
