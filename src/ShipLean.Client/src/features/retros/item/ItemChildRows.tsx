import { Link } from 'react-router-dom';
import { RETRO_CATEGORIES, type RetroCategory, type RetroItemDto } from '../../../api/retros';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { retroCategoryColours, retroCategoryLabels } from '../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../testIds';

/** A Feature's stories and bugs, one row each with its counts in the board's category order and what is still to decide. Nothing for an item with no children. */
export function ItemChildRows({ projectKey, item }: { projectKey: string; item: RetroItemDto }) {
  if (item.children.length === 0) return null;
  return (
    <section data-testid={ids.retroItemChildRows} className="grid gap-2">
      <div className="flex flex-wrap items-baseline gap-x-3">
        <h2 className="text-[length:var(--text-md)] font-semibold">Stories and bugs</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Their own retros. The Feature&apos;s retro above was compiled from them.
        </span>
      </div>
      <div className="overflow-x-auto rounded-control border border-edge bg-panel">
        <table className="w-full border-collapse text-[length:var(--text-sm)]">
          <thead>
            <tr className="border-b border-edge text-left text-ink-muted">
              <th className="px-3 py-2 font-medium">Item</th>
              {RETRO_CATEGORIES.map((category) => (
                <th key={category} className="px-3 py-2 text-right font-medium whitespace-nowrap">
                  <span className="inline-flex items-center gap-1.5">
                    <span
                      className="size-2 rounded-[2px]"
                      style={{ background: retroCategoryColours[category] }}
                    />
                    {retroCategoryLabels[category]}
                  </span>
                </th>
              ))}
              <th className="px-3 py-2 font-medium">Decided</th>
            </tr>
          </thead>
          <tbody>
            {item.children.map((child) => (
              <tr
                key={child.item.id}
                data-testid={ids.retroItemChildRow}
                data-item-key={child.item.key}
                className="border-b border-edge/60 last:border-b-0"
              >
                <td className="px-3 py-2">
                  <div className="flex min-w-0 items-center gap-2">
                    <TypeIcon type={child.item.type.id} />
                    <span className="font-mono text-ink-muted">{child.item.key}</span>
                    <Link
                      className="min-w-0 truncate text-ink no-underline hover:underline"
                      to={routePath('retroItem', { key: projectKey, itemKey: child.item.key })}
                    >
                      {child.item.title}
                    </Link>
                    {child.totals.toDecide > 0 ? (
                      <span
                        data-to-decide=""
                        className="rounded-full bg-surface-2 px-2 text-[length:var(--text-xs)] whitespace-nowrap"
                      >
                        {child.totals.toDecide} to decide
                      </span>
                    ) : null}
                  </div>
                </td>
                {RETRO_CATEGORIES.map((category: RetroCategory) => (
                  <td
                    key={category}
                    data-category={category}
                    className="px-3 py-2 text-right tabular-nums"
                  >
                    {child.totals.counts[category]}
                  </td>
                ))}
                <td className="px-3 py-2 whitespace-nowrap tabular-nums">
                  {child.totals.decided}/{child.totals.decidable}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
