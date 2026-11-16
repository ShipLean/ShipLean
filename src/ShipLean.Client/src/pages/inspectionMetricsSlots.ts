import type { ProjectDto } from '@shiplean/core/api/projects';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import type { InspectionViewProps } from '../features/inspectionMetrics/InspectionViewProps';
import {
  queryFromSearch,
  queryToSearch,
  type InspectionView,
} from '../features/inspectionMetrics/inspectionMetricsQuery';
import type { SetSearchParams } from './settingsDialogProps';

// Inspection mounts one slot, the view the address names (overview when absent or unknown). A view's change to the query
// keeps the open item (`item`) and replaces the history entry.
export function inspectionMetricsSlots(
  project: ProjectDto,
  view: InspectionMetricsViewDto,
  params: URLSearchParams,
  setParams: SetSearchParams,
): Partial<Record<InspectionView, InspectionViewProps>> {
  const query = queryFromSearch(params);
  const props: InspectionViewProps = {
    project,
    view,
    query,
    onQueryChange: (patch) => {
      const next = queryToSearch({ ...query, ...patch });
      const item = params.get('item');
      if (item !== null) next.set('item', item);
      setParams(next, { replace: true });
    },
  };
  return { [query.view]: props };
}
