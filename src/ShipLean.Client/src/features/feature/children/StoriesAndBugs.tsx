import { Chip } from '../../../components/Chip';
import { useChildEvents } from '../../workItem/tasks/useChildEvents';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';
import { ChildRow } from './ChildRow';
import { closedCount, storiesAndBugsOf } from './storiesAndBugs';

// The Feature's stories and bugs, open first. Each is its own work item; real-time child changes refetch the Feature.
export function StoriesAndBugs({ item, projectKey }: FeatureRegionProps) {
  useChildEvents(item.projectId, item.key, item);
  const children = storiesAndBugsOf(item.children);
  const { closed, total } = closedCount(item.children);
  return (
    <section data-testid={ids.storiesAndBugs} aria-label="Stories and bugs" className="grid gap-2">
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">Stories and bugs</h2>
        <span data-testid={ids.childrenCount}>
          <Chip tone={total > 0 && closed === total ? 'ok' : 'muted'}>
            {closed}/{total}
          </Chip>
        </span>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Each is its own work item
        </span>
      </header>
      <div className="rounded-control border border-edge bg-panel">
        {children.length === 0 ? (
          <p
            data-testid={ids.childrenEmpty}
            className="m-0 p-3 text-center text-[length:var(--text-sm)] text-ink-muted"
          >
            No stories or bugs yet. Add them from the backlog.
          </p>
        ) : (
          <ul className="m-0 grid list-none p-0">
            {children.map((child) => (
              <ChildRow key={child.id} child={child} projectKey={projectKey} />
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
