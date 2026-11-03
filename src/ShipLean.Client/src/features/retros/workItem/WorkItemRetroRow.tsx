import { Link } from 'react-router-dom';
import type { WorkItemRetroRowDto } from '../../../api/retros';
import { RETRO_CATEGORIES } from '../../../api/retros';
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
  return (
    <tr data-testid={ids.workItemRetroRow} data-retro-id={row.retroId} className="hover:bg-sunken">
      <td className={cell}>
        <Link
          to={routePath('retroItem', { key: projectKey, itemKey }, { retro: row.retroId })}
          className="font-medium text-ink hover:underline"
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
