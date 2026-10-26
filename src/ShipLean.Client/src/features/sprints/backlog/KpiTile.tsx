import type { ReactNode } from 'react';

interface KpiTileProps {
  testId: string;
  value: string;
  unit?: string;
  label: ReactNode;
}

/** One figure of the Sprint backlog's row: the value with its unit, and what it measures underneath. */
export function KpiTile({ testId, value, unit, label }: KpiTileProps) {
  return (
    <div className="min-w-24 rounded-md border border-edge bg-panel px-3 py-1.5">
      <div data-testid={testId} className="text-[length:var(--text-xl)] font-semibold tabular-nums">
        {value}
        {unit ? (
          <small className="ml-1 text-[length:var(--text-sm)] font-normal text-ink-muted">
            {unit}
          </small>
        ) : null}
      </div>
      <div className="text-[length:var(--text-xs)] text-ink-muted">{label}</div>
    </div>
  );
}
