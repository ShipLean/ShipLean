import type { ReactNode } from 'react';
import { StateDot } from '../../components/StateDot';
import { Chip } from '../../components/Chip';
import { TypeIcon } from '../../components/TypeIcon';
import type { RetroPointDto } from '../../api/retros';
import { AuthorChip } from './AuthorChip';
import { retroSharedTestIds as ids } from './testIds';
import { ThemeChip } from './ThemeChip';

/** One retro point as the By category columns and Search show it. The screens pass their buttons (+ Action item, archive, move theme) through `actions`. */
export function PointRow({
  point,
  actions,
}: {
  point: RetroPointDto;
  actions?: (point: RetroPointDto) => ReactNode;
}) {
  const { workItem, action } = point;
  return (
    <article
      data-testid={ids.retroPointRow}
      data-category={point.category}
      data-feature={point.isFeature ? 'true' : 'false'}
      className={`grid gap-1.5 rounded-sm border border-edge bg-panel px-2.5 py-2 ${point.isFeature ? 'border-l-[3px] border-l-signal' : ''}`}
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
        {point.isFeature ? <Chip tone="accent">Feature</Chip> : null}
        {workItem ? (
          <>
            <TypeIcon type={workItem.type.id} />
            <span className="font-mono">{workItem.key}</span>
          </>
        ) : (
          <span>Sprint</span>
        )}
        <AuthorChip author={point.author} />
        <ThemeChip theme={point.theme} recurring={point.recurring} />
        <span className="flex-1" />
        {action ? (
          <span
            title={action.title}
            className="inline-flex h-5 items-center gap-1 rounded-xs border border-edge bg-panel px-1.5 whitespace-nowrap text-ink"
          >
            <TypeIcon type="action" />
            <span className="font-mono">{action.key}</span>
            <StateDot category={action.state.category} name={action.state.name} />
          </span>
        ) : null}
        {actions ? actions(point) : null}
      </footer>
    </article>
  );
}
