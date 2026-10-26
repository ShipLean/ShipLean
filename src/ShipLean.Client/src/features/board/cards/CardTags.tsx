import type { CardTagDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';
import { tagStyle } from './cardStyle';

// The card's tags in the board's tag colours.
export function CardTags({ tags }: { tags: CardTagDto[] }) {
  return (
    <>
      {tags.map((tag) => (
        <span
          key={tag.name}
          data-testid={ids.cardTag}
          data-colour={tag.colour ?? 'none'}
          className="inline-flex h-4 items-center rounded-[var(--radius-xs)] px-1.5 text-[length:var(--text-2xs)] font-medium whitespace-nowrap text-ink"
          style={tagStyle(tag.colour)}
        >
          {tag.name}
        </span>
      ))}
    </>
  );
}
