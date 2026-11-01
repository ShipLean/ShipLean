import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

// TODO: replace with the real Action items table.
export function ActionItemsTable(props: RetroInsightsSlotProps) {
  void props;
  return <div data-testid={ids.retroActionsTable} />;
}
