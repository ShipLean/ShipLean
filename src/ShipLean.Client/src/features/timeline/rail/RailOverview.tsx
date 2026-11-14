import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { timelineTestIds as ids } from '../testIds';
import type { TimelineRailProps } from '../TimelineRailProps';
import { projectKeyOf } from './railLookup';

const guide: [string, string][] = [
  ['Bars', 'run from Active to Resolved, one solid bar per task'],
  ['Brackets', 'span a story, from its first task going Active to its last going Resolved'],
  ['Diamonds', 'mark a task closed with no start recorded'],
  ['Review dots', 'sit after a bar, one per review, joined to the bar by a thin line'],
  ['Arrows', 'join a task to the one it waited on; dashed red means it blocked the task part-way'],
  ['Critical path', 'once the Feature is Done, the chain that set its finish'],
];

/** What the chart means, the items not yet started while the Feature runs, and the agent run card. */
export function RailOverview({ view }: Pick<TimelineRailProps, 'view'>) {
  const projectKey = projectKeyOf(view);
  return (
    <div data-testid={ids.railOverview} className="grid gap-4 p-4">
      <section>
        <h2 className="m-0 mb-2 text-[length:var(--text-sm)] font-semibold">Reading the chart</h2>
        <dl className="m-0 grid gap-1.5 text-[length:var(--text-sm)]">
          {guide.map(([term, text]) => (
            <div key={term}>
              <dt className="inline font-medium">{term}</dt> <dd className="inline m-0">{text}.</dd>
            </div>
          ))}
        </dl>
      </section>
      {view.notStarted.length > 0 && (
        <section data-testid={ids.railNotStarted}>
          <h2 className="m-0 mb-2 text-[length:var(--text-sm)] font-semibold">
            Not started ({view.notStarted.length})
          </h2>
          <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
            {view.notStarted.map((item) => (
              <li key={item.key}>
                <Link
                  to={routePath('workItem', { key: projectKey, itemKey: item.key })}
                  className="text-signal"
                >
                  {item.key}
                </Link>{' '}
                {item.title}
              </li>
            ))}
          </ul>
        </section>
      )}
      <button
        type="button"
        disabled
        className="rounded-md border border-edge bg-panel p-3 text-left text-[length:var(--text-sm)] text-ink-muted"
      >
        Agent run · later
      </button>
    </div>
  );
}
