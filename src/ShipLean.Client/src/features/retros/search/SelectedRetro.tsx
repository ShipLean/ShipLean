import { retrosTestIds as ids } from '../testIds';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

// TODO: replace with the real selected retro card.
export function SelectedRetro(props: RetrosSearchSlotProps) {
  void props;
  return <div data-testid={ids.retrosSelected} />;
}
