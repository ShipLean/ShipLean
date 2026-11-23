import { useState } from 'react';
import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { GanttChart } from '../GanttChart';
import { DEFAULT_TIMELINE_OPTIONS } from '../TimelineOptions';

/** A GanttChart that keeps its own selection, so a spec can click to select and click to clear. */
export function StatefulGanttChart({ view }: { view: FeatureTimelineViewDto }) {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <GanttChart
      view={view}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected={selected}
      onSelect={setSelected}
    />
  );
}
