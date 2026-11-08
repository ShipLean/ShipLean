import type { ReactNode } from 'react';
import type { RetroKpisDto } from '../../../api/retros';
import { retrosTestIds as ids } from '../testIds';
import type { RailSheet } from './RetroRailSheets';

function NumberButton({
  testId,
  value,
  label,
  opens,
  pressed,
  onClick,
}: {
  testId: string;
  value: ReactNode;
  label: string;
  opens: boolean;
  pressed?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      data-testid={testId}
      aria-pressed={pressed}
      aria-haspopup={opens ? 'dialog' : undefined}
      className="relative grid min-h-[52px] min-w-0 cursor-pointer content-start gap-px rounded-sm border border-edge bg-canvas px-2 py-1.5 text-left text-ink aria-pressed:border-signal aria-pressed:bg-signal-soft"
      onClick={onClick}
    >
      <span className="text-[18px] leading-tight font-bold tabular-nums">{value}</span>
      <span className="text-[length:var(--text-xs)] leading-tight text-ink-muted">{label}</span>
      {opens ? (
        <span
          aria-hidden="true"
          className="absolute top-1 right-[7px] text-[length:var(--text-md)] text-ink-muted"
        >
          ›
        </span>
      ) : null}
    </button>
  );
}

/**
 * The retro header's four numbers at touch (D2, D3): one row of 52px buttons. Retros, Recurring themes and Action items
 * open the rail's section as a sheet (marked ›); Points to decide turns To decide on and off and shows pressed while on.
 */
export function RetroNumbers({
  kpis,
  decide,
  onOpen,
  onToggleDecide,
}: {
  kpis: RetroKpisDto;
  decide: boolean;
  onOpen: (sheet: RailSheet) => void;
  onToggleDecide: () => void;
}) {
  const small = 'text-[length:var(--text-sm)] font-medium text-ink-muted';
  return (
    <section
      data-testid={ids.retroNumbers}
      aria-label="Retro summary"
      className="grid grid-cols-4 gap-1.5"
    >
      <NumberButton
        testId={ids.retroNumberRetros}
        value={
          <>
            {kpis.withRetro} <small className={small}>/ {kpis.finished}</small>
          </>
        }
        label="Retros"
        opens
        onClick={() => onOpen('coverage')}
      />
      <NumberButton
        testId={ids.retroNumberRecurring}
        value={kpis.recurringThemes}
        label="Recurring themes"
        opens
        onClick={() => onOpen('recurring')}
      />
      <NumberButton
        testId={ids.retroNumberToDecide}
        value={<span className="text-caution">{kpis.pointsToDecide}</span>}
        label="Points to decide"
        opens={false}
        pressed={decide}
        onClick={onToggleDecide}
      />
      <NumberButton
        testId={ids.retroNumberActions}
        value={
          <>
            {kpis.actionItemsRaised} <small className={small}>new</small>
          </>
        }
        label="Action items"
        opens
        onClick={() => onOpen('actions')}
      />
    </section>
  );
}
