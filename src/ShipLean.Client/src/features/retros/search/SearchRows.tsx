import { retrosTestIds as ids } from '../testIds';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

// TODO: replace with the real search result rows.
export function SearchRows(props: RetrosSearchSlotProps) {
  void props;
  return <div data-testid={ids.retrosRows} />;
}
