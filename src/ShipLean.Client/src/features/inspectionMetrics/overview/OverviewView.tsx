import type { ComponentType } from 'react';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';

// TODO: the real view
export const OverviewView: ComponentType<InspectionViewProps> = () => (
  <section data-testid={ids.overview} />
);
