import { pagesTestIds as ids } from '../../pages/testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

// TODO: replace with the real Changes and impact cards.
export function ChangesImpact(props: RetroInsightsSlotProps) {
  void props;
  return <div data-testid={ids.slotRetroChangesImpact} />;
}
