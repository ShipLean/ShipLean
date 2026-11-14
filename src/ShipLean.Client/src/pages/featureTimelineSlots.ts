import type { ProjectDto } from '@shiplean/core/api/projects';
import {
  optionsFromQuery,
  optionsToQuery,
  selectedFromQuery,
} from '../features/timeline/TimelineOptions';
import type { FeatureTimelineSlotProps } from '../slots/FeatureTimelineSlot';
import type { SetSearchParams } from './settingsDialogProps';

// The timeline's options and selection live in the address; changing them keeps the rest of the query (`from`) and
// replaces the history entry.
export function featureTimelineSlots(
  project: ProjectDto,
  itemKey: string,
  params: URLSearchParams,
  setParams: SetSearchParams,
): { featureTimeline: FeatureTimelineSlotProps } {
  const search = params.toString();
  const rebuild = (next: URLSearchParams, selected: string | null) => {
    if (selected !== null) next.set('select', selected);
    const from = params.get('from');
    if (from !== null) next.set('from', from);
    setParams(next, { replace: true });
  };
  return {
    featureTimeline: {
      project,
      itemKey,
      options: optionsFromQuery(search),
      selected: selectedFromQuery(search),
      from: params.get('from') === 'feature' ? 'feature' : 'link',
      onOptionsChange: (next) => rebuild(optionsToQuery(next), selectedFromQuery(search)),
      onSelect: (key) => rebuild(optionsToQuery(optionsFromQuery(search)), key),
    },
  };
}
