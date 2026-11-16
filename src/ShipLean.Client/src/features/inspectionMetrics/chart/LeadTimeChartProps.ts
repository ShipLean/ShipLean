import type { InspectionPeriodDto } from '@shiplean/core/api/views';

export interface LeadTimeChartProps {
  /** The view's periods, oldest first: one bar each. */
  periods: readonly InspectionPeriodDto[];
  /** The chart's height in pixels; the design uses 200 on the Overview and 230 on Lead time. */
  height?: number;
  onPeriod(periodId: string): void;
  onDot(taskKey: string): void;
}
