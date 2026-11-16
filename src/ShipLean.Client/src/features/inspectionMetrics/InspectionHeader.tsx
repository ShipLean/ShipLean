import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { queryToSearch, type InspectionQuery } from './inspectionMetricsQuery';
import { inspectionMetricsTestIds as ids } from './testIds';

// The tabs the header offers; a sprint stays under Lead time and the JSON under Overview (design shared.js).
const TABS = [
  { view: 'overview', label: 'Overview', lit: ['overview', 'json'] },
  { view: 'leadtime', label: 'Lead time', lit: ['leadtime', 'period'] },
  { view: 'quality', label: 'Quality', lit: ['quality'] },
] as const;

/** Inspection's header: the title, the server's caveat (once the view model has loaded) and the view tabs. */
export function InspectionHeader({
  projectKey,
  query,
  caveat,
}: {
  projectKey: string;
  query: InspectionQuery;
  caveat: string | null;
}) {
  return (
    <header data-testid={ids.header} className="mb-3 flex flex-wrap items-center gap-3">
      <h1 data-testid={ids.headerTitle} className="page-title">
        Inspection
      </h1>
      <nav data-testid={ids.viewTabs} aria-label="Inspection views" className="flex gap-1">
        {TABS.map((tab) => {
          const active = (tab.lit as readonly string[]).includes(query.view);
          // A tab keeps the range and the split, and leaves the sprint behind.
          const search = queryToSearch({ ...query, view: tab.view, period: null, select: null });
          return (
            <Link
              key={tab.view}
              data-testid={ids.viewTab}
              data-view={tab.view}
              aria-current={active ? 'page' : undefined}
              to={routePath(
                'inspectionMetrics',
                { key: projectKey },
                Object.fromEntries(search.entries()),
              )}
              className={`rounded-control px-2 py-1 text-[length:var(--text-sm)] no-underline ${
                active ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted hover:text-ink'
              }`}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
      {caveat === null ? null : (
        <span
          data-testid={ids.caveat}
          className="ml-auto inline-flex items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted"
        >
          <i
            aria-hidden="true"
            className="inline-grid size-3.5 place-items-center rounded-full border border-current text-[9px] font-bold not-italic"
          >
            i
          </i>
          {caveat}
        </span>
      )}
    </header>
  );
}
