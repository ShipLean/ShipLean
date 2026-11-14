import type { StateChanger } from './RowStateMenu';
import { StoryRow, TouchStoryRow } from './StoryRow';
import type { SprintStory } from './sprintStories';
import { TaskRow, TouchTaskRow } from './TaskRow';

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

/** A story's two-line row followed by its tasks while it is open, at touch. */
export function TouchStoryWithTasks({ entry, changer, open, onToggle }: StoryWithTasksProps) {
  return (
    <>
      <TouchStoryRow entry={entry} changer={changer} open={open} onToggle={onToggle} />
      {open
        ? entry.tasks.map((task) => <TouchTaskRow key={task.id} task={task} changer={changer} />)
        : null}
    </>
  );
}
