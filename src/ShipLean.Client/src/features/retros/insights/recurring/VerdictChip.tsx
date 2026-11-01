import { Chip, type ChipTone } from '../../../../components/Chip';
import type { RetroVerdict } from '../../../../api/retros';

const VERDICT: Record<RetroVerdict, { label: string; tone: ChipTone }> = {
  helped: { label: 'Helped', tone: 'ok' },
  someEffect: { label: 'Some effect', tone: 'warn' },
  noEffect: { label: 'No effect', tone: 'danger' },
  tooEarly: { label: 'Too early', tone: 'muted' },
};

/** The verdict of a theme's last shipped change, or a muted note when none shipped. */
export function VerdictChip({ verdict }: { verdict: RetroVerdict | null }) {
  if (verdict === null) {
    return (
      <span data-verdict="none" className="text-[length:var(--text-xs)] text-ink-muted">
        no change shipped
      </span>
    );
  }
  const { label, tone } = VERDICT[verdict];
  return (
    <span data-verdict={verdict}>
      <Chip tone={tone}>{label}</Chip>
    </span>
  );
}
