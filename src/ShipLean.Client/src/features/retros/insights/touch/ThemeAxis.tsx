import type { RetroInsightColumnDto } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';

/** The line above the cards that names the strips' first and last column, and the Total column on the right. */
export function ThemeAxis({ columns }: { columns: readonly RetroInsightColumnDto[] }) {
  const first = columns[0];
  const last = columns[columns.length - 1];
  if (!first || !last) return null;
  return (
    <div
      data-testid={ids.retroThemeAxis}
      aria-hidden="true"
      className="grid grid-cols-[minmax(0,1fr)_52px] px-[13px] text-[length:var(--text-2xs)] text-ink-muted tabular-nums"
    >
      <span className="col-start-1 row-start-1 justify-self-start">{first.label}</span>
      {columns.length > 1 ? (
        <span className="col-start-1 row-start-1 justify-self-end">{last.label}</span>
      ) : null}
      <span className="col-start-2 row-start-1 justify-self-end">Total</span>
    </div>
  );
}
