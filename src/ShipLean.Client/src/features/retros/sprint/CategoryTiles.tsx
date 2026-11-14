import type {
  RetroCategory,
  RetroCategoryColumnDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';
import { retroCategoryColours, retroCategoryLabels } from '../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../testIds';

function toDecideLine(column: RetroCategoryColumnDto) {
  if (column.category === 'wentWell') return 'never needs deciding';
  return column.toDecide > 0 ? `${column.toDecide} to decide` : 'nothing to decide';
}

/**
 * The four category tiles at touch (D1, D7), in the server's fixed order: 2 × 2 on a phone, four in a row on a tablet.
 * Each shows how many points the filters let through and how many still need deciding; the picked one has its colour.
 */
export function CategoryTiles({
  columns,
  filter,
  picked,
  onPick,
}: {
  columns: RetroCategoryColumnDto[];
  filter: (point: RetroPointDto) => boolean;
  picked: RetroCategory;
  onPick: (category: RetroCategory) => void;
}) {
  return (
    <div
      role="group"
      aria-label="Category"
      data-testid={ids.retroCategoryTiles}
      className="grid grid-cols-4 gap-1.5 phone:grid-cols-2"
    >
      {columns.map((column) => {
        const colour = retroCategoryColours[column.category];
        const count = column.groups.reduce((sum, g) => sum + g.points.filter(filter).length, 0);
        const pressed = column.category === picked;
        return (
          <button
            key={column.category}
            type="button"
            data-testid={ids.retroCategoryTile}
            data-category={column.category}
            aria-pressed={pressed}
            className="grid min-h-12 min-w-0 cursor-pointer grid-cols-[minmax(0,1fr)_auto] items-center gap-x-1.5 gap-y-0.5 rounded-sm border border-l-4 border-edge border-l-(color:--cat) bg-panel px-2.5 py-1.5 text-left text-ink aria-pressed:border-(color:--cat) aria-pressed:bg-[color-mix(in_srgb,var(--cat)_12%,var(--color-panel))] aria-pressed:shadow-[inset_0_0_0_1px_var(--cat)]"
            style={{ ['--cat' as string]: colour }}
            onClick={() => onPick(column.category)}
          >
            <b className="truncate text-[length:var(--text-base)] font-semibold">
              {retroCategoryLabels[column.category]}
            </b>
            <span className="text-[length:var(--text-md)] font-semibold tabular-nums">{count}</span>
            <span
              data-to-decide={column.toDecide}
              className={`col-span-full text-[length:var(--text-xs)] ${column.toDecide > 0 ? 'font-semibold text-caution' : 'text-ink-muted'}`}
            >
              {toDecideLine(column)}
            </span>
          </button>
        );
      })}
    </div>
  );
}
