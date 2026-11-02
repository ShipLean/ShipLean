import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroThemeStatus } from '../../../../api/retros';

const STATUS: Record<RetroThemeStatus, { label: string; tone: ChipTone }> = {
  recurring: { label: 'Recurring', tone: 'warn' },
  beingAddressed: { label: 'Being addressed', tone: 'accent' },
  resolved: { label: 'Resolved', tone: 'ok' },
  quiet: { label: 'Quiet', tone: 'muted' },
};

/** A theme's status as a chip (the design's `status()`); the wrapper carries `data-status`. */
export function StatusChip({ status }: { status: RetroThemeStatus }) {
  const { label, tone } = STATUS[status];
  return (
    <span data-status={status}>
      <Chip tone={tone}>{label}</Chip>
    </span>
  );
}
