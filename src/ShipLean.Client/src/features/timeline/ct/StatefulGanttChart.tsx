import { useState } from 'react';
import { GanttChart } from '../GanttChart';
import { DEFAULT_TIMELINE_OPTIONS } from '../TimelineOptions';
import type { StatefulGanttChartProps } from './StatefulGanttChartProps';

/** CT harness: a GanttChart that keeps its own selection, so a click selects for real. */
export function StatefulGanttChart({ view }: StatefulGanttChartProps) {
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
