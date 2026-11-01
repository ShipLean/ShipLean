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
      className="grid gap-1.5"
    >
      <header className="flex items-center gap-2">
        <ThemeChip theme={group.theme} recurring={group.recurring} />
        <span className="text-[length:var(--text-xs)] text-ink-muted">{group.points.length}</span>
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
