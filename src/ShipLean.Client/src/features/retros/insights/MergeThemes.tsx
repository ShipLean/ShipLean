import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

// TODO: replace with the real Merge themes button and dialog.
export function MergeThemes(props: RetroInsightsSlotProps) {
  void props;
  return <div data-testid={ids.retroMergeThemes} />;
}
