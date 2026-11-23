import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { timelineTestIds as ids } from '../testIds';
import type { TimelineRailProps } from '../TimelineRailProps';
import { projectKeyOf } from './railLookup';
import { RailGlyph, type RailGlyphKind } from './RailGlyph';
import { TypeBadge } from '../parts/TypeBadge';

const guide: [RailGlyphKind, string, string][] = [
  ['bar', 'A task', 'from its first move to Active to its last move to Resolved'],
  ['review', 'A review', 'approved or asking for changes, joined to the bar by a thin line'],
  ['bracket', 'A story', 'from its first task Active to its last task Resolved'],
  ['noStart', 'A marker', 'a task closed with no start recorded'],
  ['arrow', 'Waited on', 'a dependency; dashed red means it blocked the task part-way'],
  ['critical', 'Critical path', 'once the Feature is Done, the chain that set its finish'],
];

/** What the chart means, and the items not yet started while the Feature runs. */
export function RailOverview({ view }: Pick<TimelineRailProps, 'view'>) {
  const projectKey = projectKeyOf(view);
  return (
    <div data-testid={ids.railOverview} className="grid gap-4 p-4">
      <section>
        <h2 className="m-0 mb-2 text-[length:var(--text-sm)] font-semibold">Reading the chart</h2>
        <ul className="m-0 grid list-none gap-2 p-0 text-[length:var(--text-sm)]">
          {guide.map(([kind, term, text]) => (
            <li key={term} className="flex items-center gap-2">
              <RailGlyph kind={kind} />
              <span>
                <b className="font-medium">{term}</b>, {text}.
              </span>
            </li>
          ))}
        </ul>
        <p
          data-testid={ids.railHint}
          className="m-0 mt-2 text-[length:var(--text-xs)] text-ink-muted"
        >
          Click a bar, a row or a review to see that item&apos;s history.
        </p>
      </section>
      {view.notStarted.length > 0 && (
        <section data-testid={ids.railNotStarted}>
          <h2 className="m-0 mb-2 text-[length:var(--text-sm)] font-semibold">
            Not started ({view.notStarted.length})
          </h2>
          <ul className="m-0 grid list-none gap-1 p-0 text-[length:var(--text-sm)]">
            {view.notStarted.map((item) => (
              <li key={item.key} className="flex items-center gap-2">
                <TypeBadge type={item.type} />
                <span className="font-mono text-[length:var(--text-xs)] text-ink-muted">
                  {item.key}
                </span>
                <Link
                  to={routePath('workItem', { key: projectKey, itemKey: item.key })}
                  aria-label={`${item.key} ${item.title}`}
                  className="text-signal"
                >
                  {item.title}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
      <section className="rounded-md border border-edge p-3 text-[length:var(--text-xs)] text-ink-muted">
        Inside a task: the agent&apos;s run (traces, tool calls, time per step) links from each bar
        once agent traces exist.
      </section>
    </div>
  );
}
