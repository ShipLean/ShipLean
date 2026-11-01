import type { RetroCategory, RetroPointDto } from '../../api/retros';
import { ActionChip } from './ActionChip';
import { retroCategoryColours, retroCategoryLabels } from './retroCategories';
import { ThemeChip } from './ThemeChip';

/** One category column of a retro card: its label and its points, each with root cause, `# theme` line and action item. */
export function RetroCardCategory({
  category,
  points,
  highlightPointId,
  showActions = true,
}: {
  category: RetroCategory;
  points: RetroPointDto[];
  highlightPointId?: string;
  showActions?: boolean;
}) {
  return (
    <section
      data-category={category}
      aria-label={retroCategoryLabels[category]}
      className="min-w-0 border-l-2 pl-2"
      style={{ borderColor: retroCategoryColours[category] }}
    >
      <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {retroCategoryLabels[category]}
      </div>
      {points.length > 0 ? (
        <ul className="m-0 mt-1 grid list-disc gap-1 pl-4 text-[length:var(--text-sm)]">
          {points.map((p) => {
            const highlighted = p.id === highlightPointId;
            return (
              <li
                key={p.id}
                data-point-id={p.id}
                data-highlighted={highlighted ? 'true' : 'false'}
                className={highlighted ? '-ml-1 rounded-sm bg-signal-soft px-1 py-0.5' : undefined}
              >
                {p.text}
                {category === 'wentBadly' && p.rootCause ? (
                  <span className="block text-[length:var(--text-xs)] text-ink-muted">
                    Root cause: {p.rootCause}
                  </span>
                ) : null}
                <ThemeChip theme={p.theme} variant="line" />
                {showActions && p.action ? (
                  <span className="mt-0.5 flex flex-wrap items-center gap-1 text-[length:var(--text-xs)]">
                    <ActionChip action={p.action} />
                  </span>
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : (
        <span className="text-[length:var(--text-xs)] text-ink-muted">—</span>
      )}
    </section>
  );
}
