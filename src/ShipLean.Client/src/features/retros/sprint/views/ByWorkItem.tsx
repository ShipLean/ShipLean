import { retrosTestIds as ids } from '../../testIds';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';

// TODO: replace with the real By work item view.
export function ByWorkItem(props: SprintRetroSlotProps) {
  void props;
  return <div data-testid={ids.retroCardList} />;
}
