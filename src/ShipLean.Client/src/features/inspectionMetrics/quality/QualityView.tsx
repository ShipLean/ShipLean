import type { ComponentType } from 'react';
import type { InspectionRange } from '@shiplean/core/api/views';
import { OVERLINE } from '../overview/overviewStyles';
import type { InspectionViewProps } from '../InspectionViewProps';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { QualityBugs } from './QualityBugs';
import { QualityCards } from './QualityCards';
import { QualityChart, QualityChartAxis, QualityLegend } from './QualityChart';
import { QualityRows } from './QualityRows';

const RANGES = (unit: string): readonly (readonly [InspectionRange, string])[] => [
  ['last6', `Last 6 ${unit}s`],
  ['quarter', 'This quarter'],
  ['lastQuarter', 'Last quarter'],
  ['all', `All ${unit}s`],
];

/**
 * ?view=quality (design/screens/inspection.html?view=quality, SHIP-1398 QK3): escaping bugs per sprint stacked by severity,
 * per 100 merged PRs, a row per sprint with quarter roll-ups, and every bug in the range with its Caused by. With no bug in
 * the project the view model's quality is null and the view says so.
 */
export const QualityView: ComponentType<InspectionViewProps> = ({
  project,
  view,
  query,
  onQueryChange,
}) => {
  const unit = view.cadence === 'weeks' ? 'week' : 'sprint';
  const head = (
    <div className="flex flex-wrap items-baseline gap-2">
      <h2 className="m-0 text-[length:var(--text-lg)] font-semibold">Quality</h2>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        escaping bugs: every Bug, unless it is marked Found before release
      </span>
    </div>
  );

  if (view.quality === null || view.bugs === null) {
    return (
      <section data-testid={ids.quality} className="grid gap-3">
        {head}
        <p data-testid={ids.qualityEmpty} className="m-0 text-ink-muted">
          No bugs yet. Escaping bugs show here once the project has bugs.
        </p>
      </section>
    );
  }

  return (
    <section data-testid={ids.quality} className="grid gap-3">
      {head}
      <div className="flex flex-wrap items-center gap-3">
        <select
          data-testid={ids.rangePicker}
          aria-label="Range"
          className="field w-auto"
          value={query.range}
          onChange={(event) => {
            const picked = RANGES(unit).find(([value]) => value === event.target.value);
            if (picked !== undefined) onQueryChange({ range: picked[0] });
          }}
        >
          {RANGES(unit).map(([value, label]) => (
            <option key={value} data-testid={ids.rangeOption} value={value}>
              {label}
            </option>
          ))}
        </select>
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          by {unit}, counted in the {unit} the bug was filed or in their cause&apos;s {unit}
        </span>
      </div>

      <QualityCards headline={view.headline} range={view.quality} />
      <section className="rounded-md border border-edge bg-panel">
        <header className="flex flex-wrap items-baseline gap-2 border-b border-edge px-3 py-2">
          <h3 className="m-0 text-[length:var(--text-md)] font-semibold">
            Escaping bugs by {unit}
          </h3>
          <span className="text-[length:var(--text-sm)] text-ink-muted">
            small number: per 100 merged PRs
          </span>
          <span className="ml-auto">
            <QualityLegend />
          </span>
        </header>
        <div className="p-3">
          <QualityChart periods={view.periods} />
          <QualityChartAxis periods={view.periods} />
        </div>
      </section>
      <QualityRows periods={view.periods} quarters={view.quarters} unit={unit} />
      <QualityBugs projectKey={project.key} bugs={view.bugs} periods={view.periods} />
      <aside className="grid gap-4 text-[length:var(--text-sm)] text-ink-muted md:grid-cols-3">
        <section className="grid gap-1.5">
          <span className={OVERLINE}>Found before release</span>
          <p className="m-0">
            A field on the Bug form, off by default. Tick it when the bug was caught before its
            change reached users: in review, in testing, by the agent that wrote it. A ticked bug
            isn&apos;t counted as escaping. Agents set it with{' '}
            <code>found_before_release: true</code> when they file a bug in their own review.
          </p>
        </section>
        <section className="grid gap-1.5">
          <span className={OVERLINE}>Caused by</span>
          <p className="m-0">
            A link from a bug to the task that introduced it. Once a bug has one, it counts in the{' '}
            {unit} its cause ended in instead of the {unit} it was filed.
          </p>
        </section>
        <section className="grid gap-1.5">
          <span className={OVERLINE}>Per 100 merged PRs</span>
          <p className="m-0">
            Pull requests linked to an item in this project, by the {unit} they merged. Unlinked PRs
            are left out.
          </p>
        </section>
      </aside>
    </section>
  );
};
