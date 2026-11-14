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
    <section data-testid={featureTestIds.featureTimelineSection} className="grid gap-2">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Timeline</h2>
        <Link
          to={routePath(
            'featureTimeline',
            { key: projectKey, itemKey: item.key },
            { from: 'feature' },
          )}
          className="text-[length:var(--text-sm)] text-signal no-underline hover:underline max-[900px]:hidden"
        >
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
