import { retrosTestIds as ids } from '../testIds';
import type { SprintRetroSlotProps } from './SprintRetroSlotProps';

// TODO: replace with the real sprint retro header (title and KPIs).
export function SprintRetroHeader(props: SprintRetroSlotProps) {
  void props;
  return <div data-testid={ids.retroHeader} />;
}
