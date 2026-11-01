import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** What keeps working: the Went well themes with their point count and the newest point's text. */
export function KeepsWorking({ insights }: RetroInsightsSlotProps) {
  return (
    <section
      data-testid={ids.retroKeepsWorking}
      className="rounded-frame border border-edge bg-panel"
    >
      <header className="flex items-baseline gap-3 px-4 py-3">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">What keeps working</h2>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          Went well, to keep in the planning guide
        </span>
      </header>
      <table className="w-full border-collapse">
        <thead>
          <tr>
            <th className={HEAD}>Theme</th>
            <th className={`${HEAD} text-right`}>Mentions</th>
            <th className={HEAD}>Latest</th>
          </tr>
        </thead>
        <tbody>
          {insights.keepsWorking.map((row) => (
            <tr key={row.theme.id} data-theme-id={row.theme.id} className="border-t border-edge">
              <td className="px-3 py-2 text-[length:var(--text-sm)] font-semibold">
                {row.theme.name}
              </td>
              <td className="px-3 py-2 text-right tabular-nums">{row.total}</td>
              <td className="px-3 py-2 text-[length:var(--text-sm)] text-ink-muted">
                {row.latest ?? ''}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
