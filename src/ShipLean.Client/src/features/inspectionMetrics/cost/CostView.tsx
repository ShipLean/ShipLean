import type { ComponentType } from 'react';
import { postInspectionMetricsTaskOpened } from '@shiplean/core/api/usageApi';
import type { InspectionRange } from '@shiplean/core/api/views';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { CostCards } from './CostCards';
import { CostChart } from './CostChart';
import { CostByFeature, CostMostTokens } from './CostMostTokens';
import { CostRows } from './CostRows';

const RANGES = (unit: string): readonly (readonly [InspectionRange, string])[] => [
  ['last6', `Last 6 ${unit}s`],
  ['quarter', 'This quarter'],
  ['lastQuarter', 'Last quarter'],
  ['all', `All ${unit}s`],
];

/**
 * ?view=cost (design/screens/inspection.html?view=cost, SHIP-1398 CK2, CK4): tokens per task and per merged PR by sprint,
 * the tasks with the most tokens and tokens by Feature. The view model's cost is null when agent runs are off for the
 * instance or the project, and the view says so; with cost unavailable (the store cannot total tokens per task) it shows
 * the server's note and no numbers. Every number is a server label.
 */
export const CostView: ComponentType<InspectionViewProps> = ({
  project,
  view,
  query,
  onQueryChange,
}) => {
  const unit = view.cadence === 'weeks' ? 'week' : 'sprint';
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

  const cost = view.cost;
  if (!cost.available) {
    return (
      <section data-testid={ids.cost} className="grid gap-3">
        {head}
        <p data-testid={ids.costUnavailable} className="m-0 text-ink-muted">
          {cost.note}
        </p>
      </section>
    );
  }

  return (
    <section data-testid={ids.cost} className="grid gap-3">
      {head}
      <div className="grid gap-1 rounded-panel border border-edge border-l-[3px] border-l-signal bg-panel px-4 py-3 text-[length:var(--text-sm)]">
        <b>Tokens only in v1</b>
        <span>
          Read from each task&apos;s agent runs; no money in v1. A run linked to several tasks
          splits its tokens evenly over the tasks it is most strongly linked to, so a {unit}&apos;s
          total counts each run once. Tasks done without an agent have no runs and are left out of
          the mean, not counted as zero.
        </span>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <select
          data-testid={ids.rangePicker}
          aria-label="Range"
          className="field w-auto"
          value={query.range}
          onChange={(event) => {
            const picked = RANGES(unit).find(([value]) => value === event.target.value);
            if (picked !== undefined) onQueryChange({ range: picked[0] });
          }}
        >
          {RANGES(unit).map(([value, label]) => (
            <option key={value} data-testid={ids.rangeOption} value={value}>
              {label}
            </option>
          ))}
        </select>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          by {unit}, counted in the {unit} the task&apos;s lead time ended
        </span>
      </div>

      <CostCards cost={cost} headline={view.headline} />
      <section className="rounded-md border border-edge bg-panel">
        <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
          <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
            Tokens per task by {unit}
          </h3>
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            the mean over tasks with agent runs
          </span>
        </header>
        <div className="p-3">
          <CostChart periods={view.periods} costs={cost.periods} />
        </div>
      </section>
      <CostRows periods={view.periods} costs={cost.periods} quarters={cost.quarters} unit={unit} />
      <div className="grid grid-cols-2 gap-3 touch:grid-cols-1">
        <CostMostTokens
          projectKey={project.key}
          tasks={cost.mostTokens}
          onOpen={() => {
            // The usage call never blocks or fails the page.
            postInspectionMetricsTaskOpened(project.id, 'mostTokens').catch(() => undefined);
          }}
        />
        <CostByFeature features={cost.byFeature} />
      </div>
    </section>
  );
};
