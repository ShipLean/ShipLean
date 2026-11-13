import type { RetroPointDto } from '@shiplean/core/api/retros';
import { TypeIcon } from '../../../components/TypeIcon';
import { AuthorChip } from '../AuthorChip';
import { ItemLink } from '../../itemPeek/ItemLink';

export function NoteMeta({
  point,
  showItem,
  showAuthor,
}: {
  point: RetroPointDto;
  showItem: boolean;
  showAuthor: boolean;
}) {
  return (
    <div className="flex min-w-0 flex-wrap items-center gap-[5px] text-[length:var(--text-xs)] text-ink-muted touch:text-[length:var(--text-sm)]">
      {showItem && point.workItem ? (
        <>
          <TypeIcon type={point.workItem.type.id} />
          <ItemLink
            itemKey={point.workItem.key}
            title={point.workItem.title}
            className="font-mono whitespace-nowrap text-inherit no-underline hover:underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:min-w-[var(--touch-target)] touch:items-center"
          >
            {point.workItem.key}
          </ItemLink>
        </>
      ) : null}
      {showAuthor ? <AuthorChip author={point.author} /> : null}
    </div>
  );
}
