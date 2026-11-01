import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

// TODO: replace with the real What keeps working table.
export function KeepsWorking(props: RetroInsightsSlotProps) {
  void props;
  return <div data-testid={ids.retroKeepsWorking} />;
}
