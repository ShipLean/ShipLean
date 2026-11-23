import { timelineTestIds as ids } from '../testIds';

export interface RailJsonGuideProps {
  featureKey: string;
}

// Each row: what the chart shows, then the MCP fields that draw it (checked against toMcpJson's output).
const maps: [string, string[]][] = [
  ['Key dates strip', ['key_dates', 'gaps']],
  ['A bar', ['bars[].start', 'bars[].end', 'bars[].hours']],
  ['Its history', ['bars[].history']],
  [
    'Review markers',
    ['bars[].reviews', 'bars[].reviews[].verdict', 'bars[].reviews[].by', 'bars[].reviews[].at'],
  ],
  ['A story’s bracket', ['stories']],
  ['Arrows', ['edges', 'edges[].wait_hours', 'edges[].unblocked_at']],
  ['Outlined bars', ['critical_path']],
  ['Diamond marker', ['bars[].inferred']],
];

const overline =
  'm-0 mb-2 text-[length:var(--text-xs)] font-semibold uppercase tracking-wide text-ink-muted';

/** In JSON mode: the call that returns this view, and which field draws each part of the chart. */
export function RailJsonGuide({ featureKey }: RailJsonGuideProps) {
  return (
    <div data-testid={ids.railOverview} data-mode="json" className="grid gap-4 p-4">
      <section>
        <h2 className={overline}>The call</h2>
        <pre
          data-testid={ids.railJsonCall}
          className="m-0 whitespace-pre-wrap rounded-sm bg-sunken px-2.5 py-2 font-mono text-[11.5px] leading-normal"
        >{`feature_timeline({ key: "${featureKey}" })`}</pre>
        <p className="mt-2 text-[length:var(--text-xs)] text-ink-muted">
          Read-only. Any project member's agent. One call returns the whole picture; there is
          nothing to page through.
        </p>
      </section>
      <section>
        <h2 className={overline}>How the chart maps to it</h2>
        <ul
          data-testid={ids.railJsonMap}
          className="m-0 grid list-none gap-1.5 p-0 text-[length:var(--text-xs)]"
        >
          {maps.map(([label, fields]) => (
            <li key={label} className="grid grid-cols-[110px_minmax(0,1fr)] gap-1.5">
              <span>{label}</span>
              <span className="flex flex-wrap gap-x-1.5">
                {fields.map((field) => (
                  <code key={field} className="text-[10.5px]">
                    {field}
                  </code>
                ))}
              </span>
            </li>
          ))}
        </ul>
      </section>
      <section
        data-testid={ids.railJsonRule}
        className="rounded-md border border-edge bg-sunken p-3 text-[length:var(--text-xs)]"
      >
        Rule for every chart from now on: the screen and the MCP tool are built from one server view
        model, so a person and an agent can't see different numbers.
      </section>
    </div>
  );
}
