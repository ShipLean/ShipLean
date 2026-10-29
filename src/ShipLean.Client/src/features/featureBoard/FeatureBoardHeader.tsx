import { Link } from 'react-router-dom';
import { Chip } from '../../components/Chip';
import { StateDot } from '../../components/StateDot';
import { TypeIcon } from '../../components/TypeIcon';
import type { FeatureBoardHeaderDto } from '../../api/views';
import { routePath } from '../../routes';
import { SprintPill } from '../taskboard/rows/SprintPill';
import { featureBoardTestIds as ids } from './testIds';

/** The Feature's identity and the roll-up for its tree, counted from the rows the board shows. */
export function FeatureBoardHeader({
  projectKey,
  header,
}: {
  projectKey: string;
  header: FeatureBoardHeaderDto;
}) {
  const percent =
    header.pointsTotal === null || header.pointsTotal === 0
      ? 0
      : Math.round(((header.pointsClosed ?? 0) / header.pointsTotal) * 100);
  return (
    <section
      data-testid={ids.header}
      aria-label="Feature"
      className="grid gap-1.5 border-b border-edge bg-panel px-4 pt-2 pb-3"
    >
      <Link
        data-testid={ids.back}
        to={routePath('workItem', { key: projectKey, itemKey: header.key })}
        className="justify-self-start text-[length:var(--text-sm)] text-ink-muted no-underline hover:text-signal hover:underline"
      >
        ← {header.key} {header.title}
      </Link>
      <div className="flex flex-wrap items-center gap-2">
        <TypeIcon type="feature" />
        <span data-testid={ids.key} className="text-[length:var(--text-sm)]">
          {header.key}
        </span>
        <h1 data-testid={ids.title} className="m-0 text-[length:var(--text-lg)] font-semibold">
          {header.title}
        </h1>
        <span data-testid={ids.state}>
          <StateDot category={header.state.category} name={header.state.name} />
        </span>
        {header.featuresUnder > 0 ? (
          <span data-testid={ids.featuresUnder}>
            <Chip tone="accent">
              {header.featuresUnder} {header.featuresUnder === 1 ? 'Feature' : 'Features'} under it
            </Chip>
          </span>
        ) : null}
        <span className="flex-1" />
        <span className="text-[length:var(--text-xs)] text-ink-muted">Feature board</span>
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-[length:var(--text-sm)] text-ink-muted">
        <span data-testid={ids.stories}>
          <b className="text-ink">{header.storiesClosed}</b> of{' '}
          <b className="text-ink">{header.storiesTotal}</b> stories and bugs closed
        </span>
        <span data-testid={ids.points} className="inline-flex items-center gap-1.5">
          <span
            data-testid={ids.pointsBar}
            data-percent={percent}
            className="relative inline-block h-1.5 w-40 overflow-hidden rounded-[3px] bg-sunken"
          >
            <i className="absolute inset-y-0 left-0 bg-good" style={{ width: `${percent}%` }} />
          </span>
          <span>
            <b className="text-ink">{header.pointsClosed ?? 0}</b>/
            <b className="text-ink">{header.pointsTotal ?? 0}</b> pts
          </span>
        </span>
        <span data-testid={ids.tasks}>
          Tasks <b className="text-ink">{header.tasksClosed}</b>/
          <b className="text-ink">{header.tasksTotal}</b>
        </span>
        <span data-testid={ids.sprints} className="inline-flex flex-wrap items-center gap-1">
          {header.sprints.map((sprint) => (
            <SprintPill key={sprint.id} sprint={sprint} />
          ))}
          {header.anyInBacklog ? <SprintPill sprint={null} /> : null}
        </span>
      </div>
    </section>
  );
}
