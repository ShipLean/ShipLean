import type { ReactNode } from 'react';
import type { LeadTimeStatsDto } from '@shiplean/core/api/views';
import { ChangeChip } from '../leadtime/ChangeChip';
import { SPLIT_COLOURS } from '../leadtime/SplitBar';
import { inspectionMetricsTestIds as ids } from '../testIds';

function Stat({ value, label, colour }: { value: ReactNode; label: string; colour?: string }) {
  return (
    <div>
      <div className="flex items-center gap-1.5 text-[length:var(--text-xl)] font-bold tabular-nums">
        {colour !== undefined && (
          <i aria-hidden="true" className="size-2 rounded-[2px]" style={{ background: colour }} />
        )}
        {value}
      </div>
      <div className="text-[length:var(--text-sm)] text-ink-muted">{label}</div>
    </div>
  );
}

/** The sprint's numbers: mean lead time with its change, tasks ended, the three part means and those ended at Resolved. */
export function PeriodStats({ stats, unit }: { stats: LeadTimeStatsDto; unit: string }) {
  return (
    <section data-testid={ids.periodStats} className="panel flex flex-wrap gap-8 px-4 py-3">
      <Stat
        value={
          <>
            {stats.meanLabel ?? '—'} <ChangeChip pct={stats.changePctVsPrevious} unit={unit} />
          </>
        }
        label="Mean lead time"
      />
      <Stat value={stats.tasks} label="Tasks ended" />
      <Stat
        value={stats.meanActiveLabel ?? '—'}
        label="Active (mean)"
        colour={SPLIT_COLOURS.active}
      />
      <Stat
        value={stats.meanBlockedLabel ?? '—'}
        label="Blocked (mean)"
        colour={SPLIT_COLOURS.blocked}
      />
      <Stat
        value={stats.meanReworkLabel ?? '—'}
        label="Rework (mean)"
        colour={SPLIT_COLOURS.rework}
      />
      <Stat value={stats.endedAtResolved} label="Ended at Resolved" />
    </section>
  );
}
