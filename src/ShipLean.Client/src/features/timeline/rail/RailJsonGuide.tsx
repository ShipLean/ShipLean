import { timelineTestIds as ids } from '../testIds';

const fields: [string, string][] = [
  ['bars', 'one entry per task: start, end, history periods and reviews'],
  ['stories', 'the brackets: each story or bug with its task keys'],
  ['edges', 'the arrows: from, to, kind, source and wait'],
  ['critical_path', 'the keys of the critical path, in order'],
  ['key_dates', 'added, planned, execution started and finished'],
];

/** In JSON mode: which part of the chart each MCP field draws. */
export function RailJsonGuide() {
  return (
    <div data-testid={ids.railOverview} data-mode="json" className="grid gap-2 p-4">
      <h2 className="m-0 text-[length:var(--text-sm)] font-semibold">Fields and the chart</h2>
      <dl className="m-0 grid gap-1.5 text-[length:var(--text-sm)]">
        {fields.map(([name, text]) => (
          <div key={name}>
            <dt className="inline">
              <code>{name}</code>
            </dt>{' '}
            <dd className="inline m-0">{text}.</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
