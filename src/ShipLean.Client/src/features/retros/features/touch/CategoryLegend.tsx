import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { retrosTestIds as ids } from '../../testIds';
import { CategoryHead } from '../CategoryHead';

/** The four category colours named once, in the fixed order, for the list's head. */
export function CategoryLegend() {
  return (
    <ul
      data-testid={ids.retroFeaturesLegend}
      className="flex flex-wrap gap-x-3 gap-y-1 text-[length:var(--text-xs)] font-medium text-ink-muted"
    >
      {RETRO_CATEGORIES.map((category) => (
        <li key={category} data-testid={ids.retroFeaturesLegendItem} data-category={category}>
          <CategoryHead category={category} />
        </li>
      ))}
    </ul>
  );
}
