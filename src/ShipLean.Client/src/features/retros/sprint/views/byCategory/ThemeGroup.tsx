import type { RetroThemeGroupDto, RetroThemeListDto } from '../../../../../api/retros';
import { PointRow } from '../../../../retroShared/PointRow';
import { ThemeChip } from '../../../../retroShared/ThemeChip';
import { retrosTestIds as ids } from '../../../testIds';
import { PointActions } from './PointActions';
import type { useRetroPointWrites } from './useRetroPointWrites';

/** One theme's points in a column: the theme (with Recurring) as the header, so the rows leave their theme out. */
export function ThemeGroup({
  group,
  themes,
  writes,
}: {
  group: RetroThemeGroupDto;
  themes: RetroThemeListDto | undefined;
  writes: ReturnType<typeof useRetroPointWrites>;
}) {
  return (
    <section
      data-testid={ids.retroThemeGroup}
      data-theme={group.theme?.id ?? 'unthemed'}
      data-recurring={group.recurring ? 'true' : 'false'}
      className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-1.5"
    >
      <header className="flex items-center gap-2 px-0.5 [&>span]:w-full [&>span]:justify-between [&>span]:gap-2 [&>span]:whitespace-normal">
        <ThemeChip theme={group.theme} recurring={group.recurring} />
      </header>
      {group.points.map((point) => (
        <div key={point.id} data-testid={ids.retroPoint} data-point-id={point.id}>
          <PointRow
            point={point}
            showTheme={false}
            actions={(p) => <PointActions point={p} themes={themes} writes={writes} />}
          />
        </div>
      ))}
    </section>
  );
}
