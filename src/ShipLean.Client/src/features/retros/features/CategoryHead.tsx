import type { RetroCategory } from '@shiplean/core/api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../retroShared/retroCategories';

/** A category's column heading: its coloured square and name. */
export function CategoryHead({ category }: { category: RetroCategory }) {
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap">
      <span
        aria-hidden="true"
        className="size-2 flex-none rounded-[2px]"
        style={{ background: retroCategoryColours[category] }}
      />
      {retroCategoryLabels[category]}
    </span>
  );
}
