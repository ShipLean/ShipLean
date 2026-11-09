import type { MouseEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import type { WorkItemRetroRowDto } from '@shiplean/core/api/retros';
import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { routePath } from '../../../routes';
import { AuthorChip } from '../../retroShared/AuthorChip';
import { CategoryCount } from '../features/CategoryCount';
import { DecidedProgress } from '../features/DecidedProgress';
import { retrosTestIds as ids } from '../testIds';

const cell = 'border-t border-edge px-2 py-[7px] align-middle text-[length:var(--text-sm)]';
const num = `${cell} text-right tabular-nums`;

/** One retro of the item: who wrote it, when, its counts per category and how much is decided; the author opens the item's page filtered to this retro. */
export function WorkItemRetroRow({
  projectKey,
  itemKey,
  row,
}: {
  projectKey: string;
  itemKey: string;
  row: WorkItemRetroRowDto;
}) {
  const { totals } = row;
  const navigate = useNavigate();
  const to = routePath('retroItem', { key: projectKey, itemKey }, { retro: row.retroId });
  // The whole row opens the retro. The author link stays the keyboard and middle-click way in, so a click that lands on
  // a link (it handles itself) or ends a text selection is left alone.
  const open = (event: MouseEvent<HTMLTableRowElement>) => {
    if ((event.target as HTMLElement).closest('a, button')) return;
    if (window.getSelection()?.toString()) return;
    void navigate(to);
  };
  return (
    <tr
      data-testid={ids.workItemRetroRow}
      data-retro-id={row.retroId}
      onClick={open}
      className="cursor-pointer hover:bg-sunken"
    >
      <td className={`${cell} max-w-40`}>
        <Link
          to={to}
          className="block min-w-0 font-medium text-ink hover:underline [&>span]:max-w-full [&_[data-agent]]:min-w-0 [&_[data-agent]]:truncate"
        >
          <AuthorChip author={row.author} />
        </Link>
        {row.partial ? (
          <span className="ml-2 text-[length:var(--text-xs)] text-ink-muted">partial</span>
        ) : null}
      </td>
      <td className={`${cell} text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted`}>
        {row.createdAtLabel}
      </td>
      {RETRO_CATEGORIES.map((category) => (
        <td key={category} className={num}>
          <CategoryCount category={category} count={totals.counts[category]} />
        </td>
      ))}
      <td className={num}>
        {totals.toDecide ? (
          <span className="inline-flex h-[18px] items-center rounded-full bg-caution/20 px-1.5 text-[length:var(--text-2xs)] font-semibold">
            {totals.toDecide}
          </span>
        ) : (
          <span className="text-ink-muted">0</span>
        )}
      </td>
      <td className={cell}>
        <DecidedProgress totals={totals} />
      </td>
    </tr>
  );
}
