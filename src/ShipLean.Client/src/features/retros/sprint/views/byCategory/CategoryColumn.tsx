import type { RetroCategoryColumnDto, RetroThemeListDto } from '../../../../../api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../../testIds';
import { ThemeGroup } from './ThemeGroup';
import type { useRetroPointWrites } from './useRetroPointWrites';

/** One category's column: its title and count, then a group per theme. */
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
  return (
    <section
      data-testid={ids.retroColumn}
      data-category={column.category}
      className="grid content-start gap-3"
    >
      <header
        className="flex items-center gap-2 border-t-2 pt-1.5 text-[length:var(--text-sm)] font-semibold text-ink"
        style={{ borderColor: retroCategoryColours[column.category] }}
      >
        <span>{retroCategoryLabels[column.category]}</span>
        <span className="font-normal text-ink-muted">{count}</span>
      </header>
      {column.groups.map((group) => (
        <ThemeGroup
          key={group.theme?.id ?? 'unthemed'}
          group={group}
          themes={themes}
          writes={writes}
        />
      ))}
    </section>
  );
}
