import type { RetroPointDto } from '../../../api/retros';
import { AuthorChip } from '../AuthorChip';
import { ItemLink } from '../../itemPeek/ItemLink';

// TODO: the finished meta line styling replaces this plain row.
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
    <div className="flex min-w-0 items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
      {showItem && point.workItem ? (
        <ItemLink itemKey={point.workItem.key} title={point.workItem.title} className="font-mono">
          {point.workItem.key}
        </ItemLink>
      ) : null}
      {showAuthor ? <AuthorChip author={point.author} /> : null}
    </div>
  );
}
