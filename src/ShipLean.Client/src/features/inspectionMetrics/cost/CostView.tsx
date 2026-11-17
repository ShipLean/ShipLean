import type { ComponentType } from 'react';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';

/**
 * ?view=cost (design/screens/inspection.html?view=cost, SHIP-1398 CK2, CK4): tokens per task from the agent runs. The view
 * model's cost is null when agent runs are off for the instance or the project, and the view says so; with cost unavailable
 * (the store cannot total tokens per task) it shows the server's note.
 */
export const CostView: ComponentType<InspectionViewProps> = ({ view }) => {
  const head = (
    <div className="flex flex-wrap items-baseline gap-2">
      <h2 className="m-0 text-[length:var(--text-lg)] font-semibold">Cost</h2>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        tokens per task, from each task&apos;s agent runs
      </span>
    </div>
  );

  if (view.cost === null) {
    return (
      <section data-testid={ids.cost} className="grid gap-3">
        {head}
        <p data-testid={ids.costOff} className="m-0 text-ink-muted">
          Agent runs are off for this project.
        </p>
      </section>
    );
  }

  // TODO: a placeholder until the Cost view lands; it shows only why tokens are unavailable.
  return (
    <section data-testid={ids.cost} className="grid gap-3">
      {head}
      {view.cost.available ? null : (
        <p data-testid={ids.costUnavailable} className="m-0 text-ink-muted">
          {view.cost.note}
        </p>
      )}
    </section>
  );
};
