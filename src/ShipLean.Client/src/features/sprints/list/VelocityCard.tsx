import { useVelocity } from '../sprintsApi';
import { sprintsTestIds as ids } from '../testIds';
import { LegendKey } from './LegendKey';
import { VelocityChart } from './VelocityChart';

/** Committed vs completed points for the project's last ended sprints (GET velocity), or why there is nothing to draw. */
export function VelocityCard({ projectId }: { projectId: string }) {
  const velocity = useVelocity(projectId);
  const data = velocity.data;
  return (
    <section
      data-testid={ids.velocity}
      className="overflow-hidden rounded-md border border-edge bg-panel"
    >
      <header className="flex flex-wrap items-center gap-3 border-b border-edge px-3 py-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Velocity</h2>
        {data && data.sprints.length > 0 ? (
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            Committed vs completed points, last {data.sprints.length} sprints
          </span>
        ) : null}
        <span className="flex-1" />
        <span className="flex gap-3 text-[length:var(--text-xs)] text-ink-muted">
          <LegendKey colour="var(--chart-committed)" label="Committed" />
          <LegendKey colour="var(--chart-completed)" label="Completed" />
        </span>
      </header>
      <div className="px-3 py-2">
        {data === undefined ? (
          <p className="m-0 text-ink-muted">
            {velocity.isError ? 'Velocity could not be loaded.' : 'Loading…'}
          </p>
        ) : data.sprints.length === 0 ? (
          <p data-testid={ids.velocityEmpty} className="m-0 text-ink-muted">
            No sprint has ended yet, so there is no velocity to chart.
          </p>
        ) : (
          <VelocityChart velocity={data} />
        )}
      </div>
    </section>
  );
}
