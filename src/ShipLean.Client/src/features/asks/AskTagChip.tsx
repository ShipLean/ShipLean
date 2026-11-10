import type { CommentAskTagDto } from '@shiplean/core/api/asks';
import { Chip, type ChipTone } from '../../components/Chip';
import { asksTestIds as ids } from './testIds';

const chips: Record<
  CommentAskTagDto['kind'],
  { text: (label: string) => string; tone: ChipTone; title: string | undefined }
> = {
  answer: {
    text: (label) => `Answer to ${label}`,
    tone: 'ok',
    title: undefined,
  },
  changed: {
    text: (label) => `${label} changed`,
    tone: 'accent',
    title: 'Posted when a settled decision changed',
  },
  about: { text: (label) => `On ${label}`, tone: 'muted', title: undefined },
};

// The chip on a Discussion comment the section posted. It links to the row it is about (#Q1 or #D3).
export function AskTagChip({ tag }: { tag: CommentAskTagDto }) {
  const chip = chips[tag.kind];
  const title = tag.kind === 'answer' ? `Posted from ${tag.label}'s Reply` : chip.title;
  return (
    <a
      href={`#${tag.label}`}
      data-testid={ids.commentAskTag}
      data-kind={tag.kind}
      title={title}
      className="no-underline"
    >
      <Chip tone={chip.tone}>{chip.text(tag.label)}</Chip>
    </a>
  );
}
