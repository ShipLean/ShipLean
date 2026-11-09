import { Link } from 'react-router-dom';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { TypeIcon } from '../../../components/TypeIcon';
import { retroCategoryLabels } from '../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../testIds';
import { quotedPointPath } from './quotedPointPath';

/** A point the action item covers, quoted read-only on a post-it, with a link back to where it was written. */
export function QuotedPoint({ projectKey, point }: { projectKey: string; point: RetroPointDto }) {
  return (
    <article
      data-testid={ids.retroActionWhyPoint}
      data-point-id={point.id}
      className="paper paper-postit paper-sheet grid min-w-0 gap-1.5 rounded-[2px] border px-2.5 py-2 text-[length:var(--text-sm)] [overflow-wrap:anywhere]"
    >
      <p className="m-0">{point.text}</p>
      <div className="flex min-w-0 flex-wrap items-center gap-[5px] text-[length:var(--text-xs)] text-ink-muted">
        <span>{retroCategoryLabels[point.category]}</span>
        <span aria-hidden="true">·</span>
        {point.workItem ? <TypeIcon type={point.workItem.type.id} /> : null}
        <Link
          to={quotedPointPath(projectKey, point)}
          data-testid={ids.retroActionWhyLink}
          className="text-inherit no-underline hover:underline"
        >
          {point.workItem ? (
            <span className="font-mono">{point.workItem.key}</span>
          ) : (
            (point.sprint?.name ?? 'Sprint') + ' retro'
          )}
        </Link>
      </div>
    </article>
  );
}
