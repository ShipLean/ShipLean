import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../testIds';
import { RowStateMenu, type StateChanger } from './RowStateMenu';
import { doneTaskCount, type SprintStory } from './sprintStories';

interface StoryRowProps {
  entry: SprintStory;
  changer: StateChanger;
  open: boolean;
  onToggle: () => void;
}

/** A story or bug: its toggle, key, state, assignee, task progress and points. */
export function StoryRow({ entry, changer, open, onToggle }: StoryRowProps) {
  const { story } = entry;
  const done = doneTaskCount(entry);
  const total = entry.tasks.length;
  return (
    <tr
      data-testid={ids.storyRow}
      data-key={story.key}
      className="border-b border-edge font-medium"
    >
      <td className="px-3 py-2">
        <span className="inline-flex items-center gap-2">
          <button
            type="button"
            data-testid={ids.rowToggle}
            aria-expanded={open}
            aria-label={open ? `Hide tasks of ${story.key}` : `Show tasks of ${story.key}`}
            className="size-5 cursor-pointer rounded-control border-0 bg-transparent p-0 text-ink-muted hover:bg-signal-soft"
            onClick={onToggle}
          >
            {open ? '▼' : '▶'}
          </button>
          <TypeIcon type={story.type.id} />
          <ItemLink itemKey={story.key} className="text-inherit no-underline hover:underline">
            {story.title}
          </ItemLink>
        </span>
      </td>
      <td className="px-3 py-2 font-normal whitespace-nowrap text-ink-muted">{story.key}</td>
      <td className="px-3 py-2 font-normal">
        <RowStateMenu item={story} changer={changer} />
      </td>
      <td className="px-3 py-2">{story.assignee ? <Avatar {...story.assignee} /> : null}</td>
      <td data-testid={ids.rowTasks} className="px-3 py-2 font-normal">
        <Chip tone={total > 0 && done === total ? 'ok' : 'muted'}>{`☑ ${done}/${total}`}</Chip>
      </td>
      <td className="px-3 py-2 text-right font-semibold tabular-nums">{story.points ?? '—'}</td>
    </tr>
  );
}
