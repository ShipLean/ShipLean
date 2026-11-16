import type { ProjectDto } from '@shiplean/core/api/projects';
import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import type { InspectionQuery } from './inspectionMetricsQuery';

// What the page gives every Inspection view: the loaded view model, the address as read, and a way to change it (a patch
// of the query; the page writes it to the address, replacing the history entry).
export interface InspectionViewProps {
  project: ProjectDto;
  view: InspectionMetricsViewDto;
  query: InspectionQuery;
  onQueryChange(patch: Partial<InspectionQuery>): void;
}
