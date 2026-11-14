import { useTouchShell } from '../../../touch/useTouchShell';
import { retrosTestIds as ids } from '../testIds';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** What keeps working: the Went well themes with their point count and the newest point's text. */
export function KeepsWorking({ insights }: RetroInsightsSlotProps) {
  const { touch } = useTouchShell();
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
      {touch ? (
        <ul className="m-0 grid list-none p-0">
          {insights.keepsWorking.map((row) => (
            <li
              key={row.theme.id}
              data-theme-id={row.theme.id}
              className="grid gap-0.5 border-t border-edge px-4 py-2.5"
            >
              <div className="flex items-baseline gap-3">
                <b className="min-w-0 flex-1 text-[length:var(--text-sm)]">{row.theme.name}</b>
                <span className="tabular-nums">{row.total}</span>
              </div>
              {row.latest ? (
                <p
                  data-testid={ids.retroKeepsWorkingLatest}
                  className="m-0 line-clamp-2 text-[length:var(--text-sm)] text-ink-muted"
                >
                  {row.latest}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      ) : (
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
      )}
    </section>
  );
}
