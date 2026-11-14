import { Avatar } from '../../../components/Avatar';
import { Chip } from '../../../components/Chip';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../testIds';
import { RowStateMenu, type StateChanger } from './RowStateMenu';
import { StatesSheet } from './StatesSheet';
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

/** A story or bug as two lines at touch: toggle, type icon and title; then key, state pill and tasks done, with points and the assignee on the right. */
export function TouchStoryRow({ entry, changer, open, onToggle }: StoryRowProps) {
  const { story } = entry;
  const done = doneTaskCount(entry);
  const total = entry.tasks.length;
  const complete = total > 0 && done === total;
  return (
    <li
      data-testid={ids.storyRow}
      data-key={story.key}
      data-flash={changer.flashing === story.key}
      className={`grid min-h-[58px] grid-cols-[32px_minmax(0,1fr)_40px] items-start gap-x-1 border-b border-edge py-2 pr-2 pl-1 transition-colors duration-700 ${changer.flashing === story.key ? 'bg-caution/15' : ''}`}
    >
      {total > 0 ? (
        <button
          type="button"
          data-testid={ids.rowToggle}
          aria-expanded={open}
          aria-label={open ? `Hide tasks of ${story.key}` : `Show tasks of ${story.key}`}
          className="-mt-1 h-[var(--touch-target)] w-8 cursor-pointer rounded-control border-0 bg-transparent p-0 text-[11px] text-ink-muted"
          onClick={onToggle}
        >
          {open ? '▼' : '▶'}
        </button>
      ) : (
        <span />
      )}
      <div className="grid min-w-0 gap-1">
        <div className="flex items-start gap-2 text-[length:var(--text-md)] leading-snug font-medium">
          <span className="mt-0.5 flex-none">
            <TypeIcon type={story.type.id} />
          </span>
          <ItemLink
            itemKey={story.key}
            className="line-clamp-2 min-h-[var(--touch-target)] min-w-0 text-inherit no-underline"
          >
            {story.title}
          </ItemLink>
        </div>
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 pl-6 text-[length:var(--text-xs)] text-ink-muted">
          <span className="font-mono">{story.key}</span>
          <StatesSheet item={story} changer={changer} />
          <span
            data-testid={ids.rowTasks}
            className={`whitespace-nowrap ${complete ? 'font-semibold text-good' : ''}`}
          >{`☑ ${done}/${total}`}</span>
        </div>
      </div>
      <div className="grid justify-items-end gap-1.5">
        <span className="text-[length:var(--text-md)] leading-snug font-semibold tabular-nums">
          {story.points ?? '—'}
        </span>
        {story.assignee ? <Avatar {...story.assignee} /> : null}
      </div>
    </li>
  );
}
