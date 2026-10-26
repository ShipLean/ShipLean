import type { StateChanger } from './RowStateMenu';
import { StoryRow } from './StoryRow';
import type { SprintStory } from './sprintStories';
import { TaskRow } from './TaskRow';

interface StoryWithTasksProps {
  entry: SprintStory;
  changer: StateChanger;
  open: boolean;
  onToggle: () => void;
}

/** A story's row followed by its tasks while it is open. */
export function StoryWithTasks({ entry, changer, open, onToggle }: StoryWithTasksProps) {
  return (
    <>
      <StoryRow entry={entry} changer={changer} open={open} onToggle={onToggle} />
      {open
        ? entry.tasks.map((task) => <TaskRow key={task.id} task={task} changer={changer} />)
        : null}
    </>
  );
}
