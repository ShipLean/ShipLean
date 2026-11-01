import type { RetroCategoryColumnDto, RetroThemeListDto } from '../../../../../api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../../testIds';
import { ThemeGroup } from './ThemeGroup';
import type { useRetroPointWrites } from './useRetroPointWrites';

/** One category's column, a card with a 3px top border in the category colour: title and count, then a group per theme. */
export function CategoryColumn({
  column,
  themes,
  writes,
}: {
  column: RetroCategoryColumnDto;
  themes: RetroThemeListDto | undefined;
  writes: ReturnType<typeof useRetroPointWrites>;
}) {
  const count = column.groups.reduce((sum, group) => sum + group.points.length, 0);
  const colour = retroCategoryColours[column.category];
  return (
    <section
      data-testid={ids.retroColumn}
      data-category={column.category}
      aria-label={retroCategoryLabels[column.category]}
      className="grid min-w-0 content-start gap-2 rounded-md border border-edge border-t-[3px] p-2"
      style={{
        borderTopColor: colour,
        background: `color-mix(in srgb, ${colour} 4%, var(--color-canvas))`,
      }}
    >
      <header className="flex justify-between px-0.5 font-semibold text-ink">
        <span>{retroCategoryLabels[column.category]}</span>
        <span className="font-normal text-ink-muted">{count}</span>
      </header>
      {column.groups.length === 0 ? (
        <p className="m-2 text-[length:var(--text-sm)] text-ink-muted">Nothing here.</p>
      ) : (
        column.groups.map((group) => (
          <ThemeGroup
            key={group.theme?.id ?? 'unthemed'}
            group={group}
            themes={themes}
            writes={writes}
          />
        ))
      )}
    </section>
  );
}
