import { Link } from 'react-router-dom';
import { RETRO_CATEGORIES } from '../../../api/retros';
import { routePath } from '../../../routes';
import { RetroQueryState } from '../RetroQueryState';
import { CategoryHead } from '../features/CategoryHead';
import { retrosTestIds as ids } from '../testIds';
import { useWorkItemRetros } from '../useRetroQueries';
import { WorkItemRetroRow } from './WorkItemRetroRow';

const head =
  'bg-sunken px-2 py-1.5 text-left text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-ink-muted';
// A heading may take two lines, so the four category columns stay narrow.
const wrapHead = '[&>span]:items-start [&>span]:text-left [&>span]:whitespace-normal';
const headNum = `${head} text-right`;

// One row per retro of the item, newest first, with its counts. A row opens the item's page in Retrospectives filtered to
// that retro, where its points are read and decided. Nothing shows while the item has none.
export function WorkItemRetroSection({
  projectId,
  projectKey,
  itemKey,
  workItemId,
}: {
  projectId: string;
  projectKey: string;
  itemKey: string;
  workItemId: string;
}) {
  const query = useWorkItemRetros(projectId, workItemId);
  return (
    <RetroQueryState query={query}>
      {({ rows }) =>
        rows.length === 0 ? null : (
          <section
            data-testid={ids.workItemRetroSection}
            aria-label="Retrospective"
            className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-2"
          >
            <header className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <h2 className="text-[length:var(--text-md)] font-semibold">Retrospectives</h2>
              <span className="text-[length:var(--text-xs)] text-ink-muted">{rows.length}</span>
              <span className="text-[length:var(--text-sm)] text-ink-muted">
                One row per retro. Open one to read it and decide its points.
              </span>
              <Link
                to={routePath('retroItem', { key: projectKey, itemKey })}
                className="ml-auto rounded-control border border-edge px-2 py-1 text-[length:var(--text-sm)] font-medium whitespace-nowrap hover:bg-sunken"
              >
                All retros for this item
              </Link>
            </header>
            <div className="overflow-x-auto rounded-panel border border-edge bg-panel">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <th className={head}>Retro</th>
                    <th className={`${head}`}>Logged</th>
                    {RETRO_CATEGORIES.map((category) => (
                      <th key={category} className={`${headNum} ${wrapHead}`}>
                        <CategoryHead category={category} />
                      </th>
                    ))}
                    <th className={headNum}>To decide</th>
                    <th className={head}>Decided</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <WorkItemRetroRow
                      key={row.retroId}
                      projectKey={projectKey}
                      itemKey={itemKey}
                      row={row}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )
      }
    </RetroQueryState>
  );
}
