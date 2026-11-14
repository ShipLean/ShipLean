import { useEffect } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { postTimelineBarSelected, postTimelineOpened } from '@shiplean/core/api/usageApi';
import { pagesTestIds } from '../features/pages/testIds';
import { GanttChart } from '../features/timeline/GanttChart';
import { TimelineHeader } from '../features/timeline/TimelineHeader';
import { TimelineJson } from '../features/timeline/TimelineJson';
import { TimelineRail } from '../features/timeline/TimelineRail';
import { TimelineToolbar } from '../features/timeline/TimelineToolbar';
import { timelineTestIds as ids } from '../features/timeline/testIds';
import { useFeatureTimeline } from '../features/timeline/timelineApi';
import type { TimelineOptions } from '../features/timeline/TimelineOptions';
import { QueryFailure } from '../pages/QueryFailure';

export interface FeatureTimelineSlotProps {
  project: ProjectDto;
  itemKey: string;
  options: TimelineOptions;
  selected: string | null;
  /** `feature` when the address says `?from=feature` (Open timeline on the Feature page), else `link`. */
  from: 'feature' | 'link';
  onOptionsChange(next: TimelineOptions): void;
  onSelect(key: string | null): void;
}

/** One Feature's timeline: header and toolbar over the chart (or the JSON) beside the rail. */
export function FeatureTimelineSlot({
  project,
  itemKey,
  options,
  selected,
  from,
  onOptionsChange,
  onSelect,
}: FeatureTimelineSlotProps) {
  const timeline = useFeatureTimeline(project.id, itemKey);
  const view = timeline.data;
  const loaded = view !== undefined;
  // One timeline.opened per load of a Feature's timeline (D20); the usage call never blocks or fails the page.
  useEffect(() => {
    if (loaded) postTimelineOpened(project.id, from).catch(() => undefined);
  }, [project.id, itemKey, loaded, from]);
  const select = (key: string | null) => {
    onSelect(key);
    if (key !== null) postTimelineBarSelected(project.id).catch(() => undefined);
  };
  if (view === undefined) {
    return timeline.isError ? (
      <div data-testid={ids.loadFailed}>
        <QueryFailure error={timeline.error} />
      </div>
    ) : (
      <p data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the timeline…
      </p>
    );
  }
  return (
    <div data-testid={pagesTestIds.slotFeatureTimeline} className="flex min-h-0 flex-col">
      <TimelineHeader view={view} projectKey={project.key} />
      <TimelineToolbar options={options} finished={view.finished} onChange={onOptionsChange} />
      <div className="grid min-h-0 grid-cols-[minmax(0,1fr)_320px]">
        <main className="min-w-0 overflow-auto">
          {options.view === 'json' ? (
            <TimelineJson view={view} />
          ) : (
            <GanttChart view={view} options={options} selected={selected} onSelect={select} />
          )}
        </main>
        <TimelineRail view={view} options={options} selected={selected} onSelect={select} />
      </div>
    </div>
  );
}
