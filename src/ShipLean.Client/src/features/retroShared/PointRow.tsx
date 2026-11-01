import type { ReactNode } from 'react';
import { TypeIcon } from '../../components/TypeIcon';
import type { RetroPointDto } from '../../api/retros';
import { ActionChip } from './ActionChip';
import { AuthorChip } from './AuthorChip';
import { FeatureChip } from './FeatureChip';
import { retroSharedTestIds as ids } from './testIds';
import { ThemeChip } from './ThemeChip';

/** One retro point as the By category columns and Search show it. The screens pass their buttons (+ Action item, archive, move theme) through `actions`. */
export function PointRow({
  point,
  actions,
  showTheme = true,
}: {
  point: RetroPointDto;
  actions?: (point: RetroPointDto) => ReactNode;
  /** False where a header above the row already names the theme (By category). Default true. */
  showTheme?: boolean;
}) {
  const { workItem, action } = point;
  return (
    <article
      data-testid={ids.retroPointRow}
      data-category={point.category}
      data-feature={point.isFeature ? 'true' : 'false'}
      className={`grid grid-cols-[minmax(0,1fr)] gap-1.5 rounded-sm border border-edge bg-panel px-2.5 py-2 ${point.isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
    >
      <p className="m-0">{point.text}</p>
      {point.category === 'wentBadly' && point.rootCause ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          <span className="text-[length:var(--text-2xs)] font-semibold tracking-wide uppercase">
            Root cause
          </span>{' '}
          {point.rootCause}
        </p>
      ) : null}
      <footer className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
        {point.isFeature ? <FeatureChip /> : null}
        {workItem ? (
          <>
            <TypeIcon type={workItem.type.id} />
            <span className="font-mono">{workItem.key}</span>
          </>
        ) : (
          <span>Sprint</span>
        )}
        <AuthorChip author={point.author} />
        {showTheme ? <ThemeChip theme={point.theme} recurring={point.recurring} /> : null}
        <span className="flex-1" />
        {action ? <ActionChip action={action} /> : null}
        {actions ? actions(point) : null}
      </footer>
    </article>
  );
}
