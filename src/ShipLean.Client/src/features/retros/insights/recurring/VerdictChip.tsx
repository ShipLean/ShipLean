import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroInsightsMode, RetroVerdict } from '../../../../api/retros';

const VERDICT: Record<RetroVerdict, { label: string; tone: ChipTone }> = {
  helped: { label: 'Helped', tone: 'ok' },
  someEffect: { label: 'Some effect', tone: 'warn' },
  noEffect: { label: 'No effect', tone: 'danger' },
  tooEarly: { label: 'Too early', tone: 'muted' },
};

/** The verdict of a theme's last shipped change, or a muted note when none shipped. */
// TODO: the verdict tooltip, worded for the mode, replaces this plain `data-mode`.
export function VerdictChip({
  verdict,
  mode,
}: {
  verdict: RetroVerdict | null;
  mode: RetroInsightsMode;
}) {
  if (verdict === null) {
    return (
      <span
        data-verdict="none"
        data-mode={mode}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        no change shipped
      </span>
    );
  }
  const { label, tone } = VERDICT[verdict];
  return (
    <span data-verdict={verdict} data-mode={mode}>
      <Chip tone={tone}>{label}</Chip>
    </span>
  );
}
