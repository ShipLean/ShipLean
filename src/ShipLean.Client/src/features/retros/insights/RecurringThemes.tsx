import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

// TODO: replace with the real Recurring themes table.
export function RecurringThemes(props: RetroInsightsSlotProps) {
  void props;
  return <div data-testid={ids.retroRecurringTable} />;
}
