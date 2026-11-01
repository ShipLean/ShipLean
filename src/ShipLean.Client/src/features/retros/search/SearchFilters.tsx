import { pagesTestIds as ids } from '../../pages/testIds';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

// TODO: replace with the real search filters.
export function SearchFilters(props: RetrosSearchSlotProps) {
  void props;
  return <div data-testid={ids.slotRetroSearchFilters} />;
}
