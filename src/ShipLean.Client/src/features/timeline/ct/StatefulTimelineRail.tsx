import type { FeatureTimelineViewDto } from '@shiplean/core/api/views';
import { useState } from 'react';
import { DEFAULT_TIMELINE_OPTIONS } from '../TimelineOptions';
import { TimelineRail } from '../TimelineRail';

/** Test harness: the real rail with its own selection state, as the chart page drives it. */
export function StatefulTimelineRail({
  view,
  initial,
}: {
  view: FeatureTimelineViewDto;
  initial: string | null;
}) {
  const [selected, setSelected] = useState<string | null>(initial);
  return (
    <TimelineRail
      view={view}
      options={DEFAULT_TIMELINE_OPTIONS}
      selected={selected}
      onSelect={setSelected}
    />
  );
}
