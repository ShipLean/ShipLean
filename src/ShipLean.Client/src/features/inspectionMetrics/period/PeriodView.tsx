import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';

// TODO: the real view
export function PeriodView(_props: InspectionViewProps) {
  return <section data-testid={ids.period} />;
}
