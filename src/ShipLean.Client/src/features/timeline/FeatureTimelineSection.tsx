import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { featureTestIds } from '../feature/testIds';
import { KeyDates } from './KeyDates';
import { timelineTestIds as ids } from './testIds';
import type { FeatureTimelineSectionProps } from './FeatureTimelineSectionProps';
import { useFeatureTimeline } from './timelineApi';

/** The Feature page's Timeline section: the key dates and a way into the full timeline. Nothing else. */
export function FeatureTimelineSection({ item, projectKey }: FeatureTimelineSectionProps) {
  const timeline = useFeatureTimeline(item.projectId, item.key);
  return (
    <section
      data-testid={featureTestIds.featureTimelineSection}
      className="grid max-w-[920px] gap-2 rounded-md border border-edge bg-panel px-3 pt-3 pb-2"
    >
      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Timeline</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          The key dates. Open the timeline for every task and what waited on what.
        </span>
        <span className="flex-1" />
        <Link
          data-testid={featureTestIds.featureTimelineOpen}
          to={routePath(
            'featureTimeline',
            { key: projectKey, itemKey: item.key },
            { from: 'feature' },
          )}
          className="inline-flex items-center gap-1 rounded-sm border border-edge px-2 py-0.5 text-[length:var(--text-xs)] text-ink no-underline hover:border-signal hover:text-signal touch:hidden"
        >
          <svg
            aria-hidden="true"
            viewBox="0 0 16 16"
            className="size-3 fill-none stroke-current"
            strokeWidth="1.5"
          >
            <path d="M9.5 2.5h4v4M6.5 13.5h-4v-4M13.5 2.5l-4.5 4.5M2.5 13.5L7 9" />
          </svg>
          Open timeline
        </Link>
      </div>
      {timeline.isError ? (
        <p data-testid={ids.loadFailed} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          The timeline could not be loaded.
        </p>
      ) : timeline.data === undefined ? (
        <div
          data-testid={ids.loading}
          aria-label="Loading the timeline"
          className="h-10 animate-pulse rounded bg-edge"
        />
      ) : (
        <KeyDates view={timeline.data} compact />
      )}
    </section>
  );
}
