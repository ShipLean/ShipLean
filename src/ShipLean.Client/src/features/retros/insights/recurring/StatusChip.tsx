import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroInsightsMode, RetroThemeStatus } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { Tip } from '../tips/Tip';
import { statusWording } from '../tips/tipWording';

const STATUS: Record<RetroThemeStatus, { label: string; tone: ChipTone }> = {
  recurring: { label: 'Recurring', tone: 'warn' },
  beingAddressed: { label: 'Being addressed', tone: 'accent' },
  resolved: { label: 'Resolved', tone: 'ok' },
  quiet: { label: 'Quiet', tone: 'muted' },
};

/** A theme's status as a chip (the design's `status()`); the wrapper carries `data-status` and `data-mode`. */
export function StatusChip({
  status,
  mode,
}: {
  status: RetroThemeStatus;
  mode: RetroInsightsMode;
}) {
  const { label, tone } = STATUS[status];
  return (
    <Tip
      data-testid={ids.retroStatusChip}
      data-status={status}
      data-mode={mode}
      tip={statusWording(status, mode)}
      className="no-underline"
    >
      <Chip tone={tone}>{label}</Chip>
    </Tip>
  );
}
