import { timelineTestIds as ids } from './testIds';
import type { KeyDatesProps } from './KeyDatesProps';

// TODO: replaced by the four key dates with their gaps.
export function KeyDates({ compact }: KeyDatesProps) {
  return <div data-testid={ids.keyDates} data-compact={compact} />;
}
