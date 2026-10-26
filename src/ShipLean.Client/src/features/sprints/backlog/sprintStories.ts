import type { WorkItemDto } from '../../../api/workItems';

export interface SprintStory {
  story: WorkItemDto;
  /** The story's tasks in this sprint as full items (their revision and state), in the server's order. A Removed task is not in the project's list, so it is not here. */
  tasks: WorkItemDto[];
}

const isStoryOrBug = (item: WorkItemDto) => item.type.id === 'story' || item.type.id === 'bug';

/**
 * A task belongs to the sprint in its own sprint field, whatever sprint its parent is in; rollover moves tasks by the
 * same rule.
 */
const inSprint = (item: WorkItemDto, sprintId: string) => item.sprint?.id === sprintId;

/** The sprint's stories and bugs in the order the server lists them (backlog order), each with its tasks in this sprint. */
export function sprintStories(items: WorkItemDto[], sprintId: string): SprintStory[] {
  const byId = new Map(items.map((item) => [item.id, item]));
  return items
    .filter((item) => isStoryOrBug(item) && inSprint(item, sprintId))
    .map((story) => ({
      story,
      tasks: story.children.flatMap((child) => {
        const task = byId.get(child.id);
        return task !== undefined && inSprint(task, sprintId) ? [task] : [];
      }),
    }));
}

/** The sprint's tasks that are not under a story or bug of this sprint (no parent, or a parent in another sprint), in list order. */
export function unparentedTasks(items: WorkItemDto[], sprintId: string): WorkItemDto[] {
  const parents = new Set(
    items.filter((item) => isStoryOrBug(item) && inSprint(item, sprintId)).map((item) => item.id),
  );
  return items.filter(
    (item) =>
      item.type.id === 'task' &&
      inSprint(item, sprintId) &&
      (item.parent === null || !parents.has(item.parent.id)),
  );
}

/** How many of a story's tasks are in the Completed category. */
export function doneTaskCount({ tasks }: SprintStory): number {
  return tasks.filter((task) => task.state.category === 'completed').length;
}
