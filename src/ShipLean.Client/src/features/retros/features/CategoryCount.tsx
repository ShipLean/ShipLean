import type { RetroCategory } from '@shiplean/core/api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../testIds';

/** One category's count: a coloured square and the number, dimmed at zero. */
export function CategoryCount({ category, count }: { category: RetroCategory; count: number }) {
  return (
    <span
      data-testid={ids.retroCategoryCount}
      data-category={category}
      data-count={count}
      title={`${retroCategoryLabels[category]}: ${count}`}
      className={`inline-flex items-center gap-1 text-[length:var(--text-sm)] tabular-nums ${count ? '' : 'text-ink-muted opacity-60'}`}
    >
      <span
        aria-hidden="true"
        className="size-2 flex-none rounded-[2px]"
        style={{ background: retroCategoryColours[category] }}
      />
      {count}
    </span>
  );
}
