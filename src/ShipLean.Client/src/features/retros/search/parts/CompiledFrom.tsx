import { TypeIcon } from '../../../../components/TypeIcon';
import type { RetroItemRefDto } from '@shiplean/core/api/retros';
import { ItemLink } from '../../../itemPeek/ItemLink';

/** The items a retro was compiled from, below its board; nothing is drawn when there are none. */
export function CompiledFrom({ items }: { items: RetroItemRefDto[] }) {
  if (items.length === 0) return null;
  return (
    <p className="m-0 flex flex-wrap items-center gap-1.5 text-[length:var(--text-xs)] text-ink-muted">
      Compiled from
      {items.map((item, i) => (
        <span key={item.id}>
          <span className="inline-flex items-center gap-1">
            <TypeIcon type={item.type.id} />
            <ItemLink
              itemKey={item.key}
              title={item.title}
              className="inline-flex items-center font-mono touch:min-h-[var(--touch-target)]"
            >
              {item.key}
            </ItemLink>
          </span>
          {i < items.length - 1 ? ',' : '.'}
        </span>
      ))}
    </p>
  );
}
