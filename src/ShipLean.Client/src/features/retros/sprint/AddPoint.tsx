import { retrosTestIds as ids } from '../testIds';
import type { SprintRetroSlotProps } from './SprintRetroSlotProps';

// TODO: replace with the real Add a point button and dialog.
export function AddPoint(props: SprintRetroSlotProps) {
  void props;
  return <div data-testid={ids.retroAddPoint} />;
}
