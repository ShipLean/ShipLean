import { Link } from 'react-router-dom';
import type { RetroFeatureRowDto } from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import { routePath } from '../../../../routes';
import { retrosTestIds as ids } from '../../testIds';
import { CategoryCount } from '../CategoryCount';
import { DecidedProgress } from '../DecidedProgress';

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;

/** One row link of four lines: type, key, state and last retro; the title and chip; the counts, what is left to decide and the Decided bar; retros and action items. */
export function PhoneFeatureRow({
  projectKey,
  row,
}: {
  projectKey: string;
  row: RetroFeatureRowDto;
}) {
  const { item, totals } = row;
  return (
    <Link
      data-testid={ids.retroFeaturesRow}
      data-key={item.key}
      to={routePath('retroItem', { key: projectKey, itemKey: item.key })}
      className="relative grid min-w-0 gap-1 border-t border-edge py-2.5 pr-7 pl-3 text-ink first:border-t-0 active:bg-sunken after:absolute after:top-1/2 after:right-2.5 after:-translate-y-1/2 after:text-lg after:text-ink-muted after:content-['›']"
    >
      <span className="flex min-w-0 items-center gap-1.5">
        <TypeIcon type={item.type.id} />
        <span className="font-mono text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
          {item.key}
        </span>
        <StateDot category={row.state.category} name={row.state.name} />
        <span className="ml-auto text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
          {row.lastRetroLabel}
        </span>
      </span>
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <b className="line-clamp-2 text-[length:var(--text-md)] leading-snug font-semibold">
          {item.title}
        </b>
        {row.noFeatureRetro ? (
          <span
            data-testid={ids.retroFeaturesNoFeatureChip}
            className="h-5 rounded-full bg-sunken px-2 text-[length:var(--text-xs)] leading-5 whitespace-nowrap"
          >
            No Feature retro yet
          </span>
        ) : null}
      </span>
      <span className="flex min-w-0 flex-wrap items-center gap-x-2.5 gap-y-1.5">
        {RETRO_CATEGORIES.map((category) => (
          <CategoryCount key={category} category={category} count={totals.counts[category]} />
        ))}
        <span className="ml-auto inline-flex items-center gap-2.5">
          {totals.toDecide ? (
            <span
              data-testid={ids.retroFeaturesToDecide}
              className="inline-flex h-5 items-center rounded-full bg-caution/20 px-1.5 text-[length:var(--text-xs)] font-semibold"
            >
              {totals.toDecide} to decide
            </span>
          ) : null}
          <DecidedProgress totals={totals} />
        </span>
      </span>
      <span
        data-testid={ids.retroFeaturesMeta}
        className="text-[length:var(--text-xs)] text-ink-muted"
      >
        {plural(row.retros, 'retro')} · {plural(totals.actions, 'action item')}
      </span>
    </Link>
  );
}
