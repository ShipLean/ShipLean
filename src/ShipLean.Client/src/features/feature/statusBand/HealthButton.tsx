import { useState } from 'react';
import type { FeatureHealth } from '@shiplean/core/api/screens';
import { ActionSheet, type SheetAction } from '../../../touch/ActionSheet';
import { featureTestIds as ids } from '../testIds';

const toneOf: Record<FeatureHealth, string> = {
  onTrack: 'bg-ok',
  atRisk: 'bg-warn',
  blocked: 'bg-danger',
};

const options: readonly { value: FeatureHealth; label: string; hint: string }[] = [
  { value: 'onTrack', label: 'On track', hint: 'Nothing in the way' },
  { value: 'atRisk', label: 'At risk', hint: 'Might slip; say why in the status line' },
  { value: 'blocked', label: 'Blocked', hint: 'Cannot move until something changes' },
];

export const healthLabel = (health: FeatureHealth | null): string =>
  options.find((option) => option.value === health)?.label ?? 'Set health';

function HealthDot({ health }: { health: FeatureHealth | null }) {
  return (
    <span
      aria-hidden="true"
      className={`inline-block size-2.5 rounded-full ${health === null ? 'border border-ink-muted' : toneOf[health]}`}
    />
  );
}

// At touch the Health control: a pill that opens the Health sheet (SHIP-907 H1, H2). Choosing writes the form's draft, as the
// desktop select does; the form's Save sends it.
export function HealthButton({
  health,
  onChange,
}: {
  health: FeatureHealth | null;
  onChange: (health: FeatureHealth | null) => void;
}) {
  const [open, setOpen] = useState(false);
  const actions: SheetAction[] = [
    ...options.map((option) => ({
      id: option.value,
      label: option.label,
      hint: option.hint,
      icon: <HealthDot health={option.value} />,
      checked: option.value === health,
      onSelect: () => onChange(option.value),
    })),
    {
      id: 'clear',
      label: 'Clear health',
      group: '',
      disabled: health === null,
      onSelect: () => onChange(null),
    },
  ];
  return (
    <>
      <button
        type="button"
        data-testid={ids.statusHealthButton}
        data-health={health ?? ''}
        aria-haspopup="dialog"
        aria-label={`Health: ${healthLabel(health)}`}
        className="inline-flex h-[var(--touch-target)] items-center gap-2 rounded-full border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink active:bg-signal-soft"
        onClick={() => setOpen(true)}
      >
        <HealthDot health={health} />
        {healthLabel(health)}
        <span aria-hidden="true" className="text-ink-muted">
          ▾
        </span>
      </button>
      <ActionSheet
        open={open}
        title="Health"
        actions={actions}
        onClose={() => setOpen(false)}
        footer={
          <span data-testid={ids.healthSheetFootnote}>Saved with the Feature. Save writes it.</span>
        }
      />
    </>
  );
}
