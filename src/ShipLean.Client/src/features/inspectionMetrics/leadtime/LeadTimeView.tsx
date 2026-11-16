import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';

// TODO: the real view
export function LeadTimeView(_props: InspectionViewProps) {
  return <section data-testid={ids.leadTime} />;
}
