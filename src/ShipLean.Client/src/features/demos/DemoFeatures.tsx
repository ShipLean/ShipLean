import { Link } from 'react-router-dom';
import { StateDot } from '../../components/StateDot';
import { TypeIcon } from '../../components/TypeIcon';
import type { DemoProps } from './DemoProps';
import { demosTestIds as ids } from './testIds';

/** The features the chapters cover, each linked, with its state. */
export function DemoFeatures({ demo }: DemoProps) {
  const { features } = demo;
  return (
    <section
      aria-label="Features covered"
      className="min-w-0 rounded-panel border border-edge bg-surface"
    >
      <header className="flex min-h-9 items-center gap-2 border-b border-edge px-3">
        <h2 className="text-[length:var(--text-base)] font-semibold">Features covered</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">{features.length}</span>
      </header>
      <ul data-testid={ids.demoFeatures} className="m-0 grid list-none gap-px p-1">
        {features.map((feature) => (
          <li
            key={feature.key}
            data-testid={ids.demoFeature}
            className="flex min-h-7.5 items-center gap-2 px-2 text-[length:var(--text-sm)] touch:min-h-[var(--touch-target)]"
          >
            <TypeIcon type="feature" />
            <Link
              to={feature.url}
              className="font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] text-signal hover:underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center"
            >
              {feature.key}
            </Link>
            <span className="min-w-0 flex-1 truncate touch:line-clamp-2 touch:whitespace-normal [overflow-wrap:anywhere]">
              {feature.title}
            </span>
            <StateDot category={feature.state.category} name={feature.state.name} />
          </li>
        ))}
      </ul>
    </section>
  );
}
