import { Link } from 'react-router-dom';
import { Chip } from '../../components/Chip';
import { StateDot } from '../../components/StateDot';
import { TypeIcon } from '../../components/TypeIcon';
import { routePath } from '../../routes';
import { KeyDates } from './KeyDates';
import { timelineTestIds as ids } from './testIds';
import type { TimelineHeaderProps } from './TimelineHeaderProps';

/** The Feature's identity and its key dates; the chart below is the story, so no findings here. */
export function TimelineHeader({ view, projectKey }: TimelineHeaderProps) {
  const { feature } = view;
  return (
    <header
      data-testid={ids.header}
      className="grid gap-1.5 border-b border-edge bg-panel px-4 pt-2 pb-3"
    >
      <Link
        data-testid={ids.headerBack}
        to={routePath('workItem', { key: projectKey, itemKey: feature.key })}
        className="justify-self-start text-[length:var(--text-sm)] text-ink-muted no-underline hover:text-signal hover:underline"
      >
        ← Back to {feature.key}
      </Link>
      <div className="flex flex-wrap items-center gap-2">
        <TypeIcon type="feature" />
        <span className="text-[length:var(--text-sm)]">{feature.key}</span>
        <h1
          data-testid={ids.headerTitle}
          className="m-0 text-[length:var(--text-lg)] font-semibold"
        >
          {feature.title}
        </h1>
        <span data-testid={ids.headerState}>
          <StateDot category={feature.stateCategory} name={feature.state} />
        </span>
        {feature.epic === null ? null : (
          <span data-testid={ids.headerEpic}>
            <Chip>{feature.epic.title}</Chip>
          </span>
        )}
        <span className="flex-1" />
        <span className="text-[length:var(--text-xs)] text-ink-muted">Timeline</span>
      </div>
      <KeyDates view={view} compact={false} />
    </header>
  );
}
