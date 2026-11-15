import { Link } from 'react-router-dom';
import { RETRO_CATEGORIES, type WorkItemRetroRowDto } from '@shiplean/core/api/retros';
import { routePath } from '../../../../routes';
import { AuthorChip } from '../../../retroShared/AuthorChip';
import { CategoryCount } from '../../features/CategoryCount';
import { DecidedProgress } from '../../features/DecidedProgress';
import { retrosTestIds as ids } from '../../testIds';

/** One retro of the item as a row link of two lines (R8): the author, "partial" and the date; then the counts, what is left to decide and the Decided bar. */
export function PhoneRetroRow({
  projectKey,
  itemKey,
  row,
}: {
  projectKey: string;
  itemKey: string;
  row: WorkItemRetroRowDto;
}) {
  const { totals } = row;
  return (
    <Link
      data-testid={ids.workItemRetroRow}
      data-retro-id={row.retroId}
      to={routePath('retroItem', { key: projectKey, itemKey }, { retro: row.retroId })}
      className="grid min-h-[var(--touch-target)] min-w-0 gap-1 border-t border-edge px-3 py-2.5 text-ink first:border-t-0 active:bg-sunken"
    >
      <span className="flex min-w-0 items-center gap-2">
        <span className="min-w-0 font-medium [&>span]:max-w-full [&_[data-agent]]:min-w-0 [&_[data-agent]]:truncate">
          <AuthorChip author={row.author} />
        </span>
        {row.partial ? (
          <span className="text-[length:var(--text-xs)] text-ink-muted">partial</span>
        ) : null}
        <span className="ml-auto text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
          {row.createdAtLabel}
        </span>
      </span>
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
