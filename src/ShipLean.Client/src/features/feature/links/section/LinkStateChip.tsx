import { Chip, type ChipTone } from '../../../../components/Chip';
import type { LinkState } from '../../../../api/linkCards';

const chips: Record<LinkState, { label: string; tone: ChipTone }> = {
  open: { label: 'Open', tone: 'ok' },
  draft: { label: 'Draft', tone: 'muted' },
  merged: { label: 'Merged', tone: 'purple' },
  closed: { label: 'Closed', tone: 'danger' },
};

// A code link's state: Open, Draft, Merged or Closed.
export function LinkStateChip({ state }: { state: LinkState }) {
  const { label, tone } = chips[state];
  return <Chip tone={tone}>{label}</Chip>;
}
