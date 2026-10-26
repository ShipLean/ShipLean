import type { CardTagDto } from '../../../api/views';
import { Chip } from '../../../components/Chip';
import { backlogTestIds as ids } from '../testIds';

// Tags in the colours the board gives them; a tag with no colour is muted.
export function TagsCell({ tags }: { tags: CardTagDto[] }) {
  return (
    <span className="inline-flex flex-wrap gap-1">
      {tags.map((tag) => (
        <span key={tag.name} data-testid={ids.tag} data-tone={tag.colour ?? 'muted'}>
          <Chip tone={tag.colour ?? 'muted'}>{tag.name}</Chip>
        </span>
      ))}
    </span>
  );
}
