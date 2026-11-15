import { Link } from 'react-router-dom';
import { RETRO_CATEGORIES, type RetroFeatureRowDto } from '@shiplean/core/api/retros';
import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import { routePath } from '../../../../routes';
import { CategoryCount } from '../../features/CategoryCount';
import { DecidedProgress } from '../../features/DecidedProgress';
import { retrosTestIds as ids } from '../../testIds';

/** One story or bug as a row link of three lines (R7): type, key, state; the title (two lines at most); the counts, what is left to decide and the Decided bar. */
export function PhoneChildRow({
  projectKey,
  child,
}: {
  projectKey: string;
  child: RetroFeatureRowDto;
}) {
  const { item, totals } = child;
  return (
    <Link
      data-testid={ids.retroItemChildRow}
      data-item-key={item.key}
      to={routePath('retroItem', { key: projectKey, itemKey: item.key })}
      className="relative grid min-h-[var(--touch-target)] min-w-0 gap-1 border-t border-edge py-2.5 pr-7 pl-3 text-ink first:border-t-0 active:bg-sunken after:absolute after:top-1/2 after:right-2.5 after:-translate-y-1/2 after:text-lg after:text-ink-muted after:content-['›']"
    >
      <span className="flex min-w-0 items-center gap-1.5">
        <TypeIcon type={item.type.id} />
        <span className="font-mono text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
          {item.key}
        </span>
        <StateDot category={child.state.category} name={child.state.name} />
      </span>
      <b className="line-clamp-2 text-[length:var(--text-md)] leading-snug font-semibold">
        {item.title}
      </b>
      <span className="flex min-w-0 flex-wrap items-center gap-x-2.5 gap-y-1.5">
        {RETRO_CATEGORIES.map((category) => (
          <CategoryCount key={category} category={category} count={totals.counts[category]} />
        ))}
        <span className="ml-auto inline-flex items-center gap-2.5">
          {totals.toDecide > 0 ? (
            <span
              data-to-decide=""
              className="inline-flex h-5 items-center rounded-full bg-caution/20 px-1.5 text-[length:var(--text-xs)] font-semibold"
            >
              {totals.toDecide} to decide
            </span>
          ) : null}
          <DecidedProgress totals={totals} />
        </span>
      </span>
    </Link>
  );
}
