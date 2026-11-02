import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroInsightsMode, RetroThemeStatus } from '../../../../api/retros';

const STATUS: Record<RetroThemeStatus, { label: string; tone: ChipTone }> = {
  recurring: { label: 'Recurring', tone: 'warn' },
  beingAddressed: { label: 'Being addressed', tone: 'accent' },
  resolved: { label: 'Resolved', tone: 'ok' },
  quiet: { label: 'Quiet', tone: 'muted' },
};

/** A theme's status as a chip (the design's `status()`); the wrapper carries `data-status` and `data-mode`. */
// TODO: the status tooltip, worded for the mode, replaces this plain `data-mode`.
export function StatusChip({
  status,
  mode,
}: {
  status: RetroThemeStatus;
  mode: RetroInsightsMode;
}) {
  const { label, tone } = STATUS[status];
  return (
    <span data-status={status} data-mode={mode}>
      <Chip tone={tone}>{label}</Chip>
    </span>
  );
}
