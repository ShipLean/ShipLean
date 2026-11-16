import { StateDot } from '../../../../components/StateDot';
import { TypeIcon } from '../../../../components/TypeIcon';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { RowToggle } from '../../rows/RowToggle';
import { taskboardTestIds as ids } from '../../testIds';
import type { ClosedStory } from './phoneLanes';

/** A closed story on a phone page (D8): one 40px line with ▶ (opens its full lane), its title, ☑ done/total tasks, how many are on this page and its state. */
export function ClosedStoryLine({
  projectKey,
  story,
  onToggle,
}: {
  projectKey: string;
  story: ClosedStory;
  onToggle: () => void;
}) {
  const parent = story.row.parent;
  if (!parent) return null;
  return (
    <div
      data-testid={ids.closedLine}
      data-row={story.rowId}
      className="flex h-10 min-w-0 items-center gap-1.5 rounded-md border border-edge bg-panel pr-2 text-[length:var(--text-sm)]"
    >
      <RowToggle collapsed onToggle={onToggle} label={parent.key} />
      <TypeIcon type={parent.type.id} />
      <ItemLink
        testId={ids.rowTitle}
        itemKey={parent.key}
        projectKey={projectKey}
        className="block min-w-0 flex-1 truncate leading-[var(--touch-target)] text-ink-muted no-underline"
      >
        {parent.title}
      </ItemLink>
      <span
        data-testid={ids.closedLineDone}
        className="whitespace-nowrap text-[length:var(--text-xs)] text-ink-muted"
      >
        ☑ {story.done}/{story.total}
      </span>
      {story.lane.tasks.length > 0 ? (
        <span
          data-testid={ids.closedLineHere}
          className="whitespace-nowrap text-[length:var(--text-xs)] text-ink-muted"
        >
          {story.lane.tasks.length} here
        </span>
      ) : null}
      <StateDot category={parent.state.category} name={parent.state.name} />
    </div>
  );
}
