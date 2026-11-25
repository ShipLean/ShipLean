import { timelineTestIds as ids } from '../testIds';

export interface RailJsonGuideProps {
  featureKey: string;
}

// Each row: what the chart shows, then the MCP fields that draw it (checked against toMcpJson's output).
// A field is a code path; plain strings are the mock-up's words between them.
type Part = { code: string } | string;
const maps: [string, Part[]][] = [
  ['Key dates strip', [{ code: 'key_dates' }, ', ', { code: 'gaps' }]],
  [
    'A bar',
    [
      { code: 'bars[]' },
      ': ',
      { code: 'bars[].start' },
      ' (first Active), ',
      { code: 'bars[].end' },
      ' (last Resolved), ',
      { code: 'bars[].hours' },
    ],
  ],
  ['Its history', [{ code: 'bars[].history' }, ': each state']],
  [
    'Review markers',
    [
      { code: 'bars[].reviews' },
      ': ',
      { code: 'bars[].reviews[].verdict' },
      ', ',
      { code: 'bars[].reviews[].by' },
      ', ',
      { code: 'bars[].reviews[].at' },
    ],
  ],
  ['A story’s bracket', [{ code: 'stories[]' }]],
  [
    'Arrows',
    [
      { code: 'edges[]' },
      ' with ',
      { code: 'edges[].wait_hours' },
      '; blocked part-way has ',
      { code: 'edges[].unblocked_at' },
    ],
  ],
  ['Outlined bars', [{ code: 'critical_path' }]],
  ['Diamond marker', [{ code: 'bars[].inferred' }, ': inferred: true']],
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
          {maps.map(([label, parts]) => (
            <li key={label} className="grid grid-cols-[110px_minmax(0,1fr)] gap-1.5">
              <span>{label}</span>
              <span>
                {parts.map((part, i) =>
                  typeof part === 'string' ? (
                    part
                  ) : (
                    <code key={i} className="text-[10.5px]">
                      {part.code}
                    </code>
                  ),
                )}
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
