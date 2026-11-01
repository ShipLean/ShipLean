import { retrosTestIds as ids } from '../../testIds';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';

// TODO: replace with the real retro rail (coverage, action items, recurring).
export function RetroRail(props: SprintRetroSlotProps) {
  void props;
  return <div data-testid={ids.retroRail} />;
}
