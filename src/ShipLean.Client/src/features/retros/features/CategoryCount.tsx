import type { RetroCategory } from '../../../api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../retroShared/retroCategories';

/** One category's count: a coloured square and the number, dimmed at zero. */
export function CategoryCount({ category, count }: { category: RetroCategory; count: number }) {
  return (
    <span
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
