import type { StateCategory } from '../../../../api/refs';
import type { WorkItemDto } from '../../../../api/workItems';
import type { SprintStory } from '../sprintStories';

export interface ReadyItem {
  item: WorkItemDto;
  /** Under the title: a story's key, a task's story (or its own key when it has none). */
  sub: string;
}

export const RAIL_CATEGORIES = ['proposed', 'inProgress', 'resolved', 'completed'] as const;
export type RailCategory = (typeof RAIL_CATEGORIES)[number];

export interface RailSummary {
  /** Unassigned and not started, in backlog order: each story, then its tasks, then the tasks with no story. */
  ready: ReadyItem[];
  /** The titles of what each person (by user id) has assigned in an In progress state. */
  activeByUser: ReadonlyMap<string, string[]>;
  /** Story and bug points by state category (tasks carry none that count). */
  points: Record<RailCategory, number>;
}

const isStarted = (category: StateCategory) => category !== 'proposed';

/** The rail's three lists from the sprint's items, grouped by state category so custom states still roll up. */
export function railSummary(stories: SprintStory[], unparented: WorkItemDto[]): RailSummary {
  const ready: ReadyItem[] = [];
  const activeByUser = new Map<string, string[]>();
  const points: Record<RailCategory, number> = {
    proposed: 0,
    inProgress: 0,
    resolved: 0,
    completed: 0,
  };
  const visit = (item: WorkItemDto, sub: string) => {
    if (!item.assignee && !isStarted(item.state.category)) ready.push({ item, sub });
    if (item.assignee && item.state.category === 'inProgress') {
      activeByUser.set(item.assignee.id, [
        ...(activeByUser.get(item.assignee.id) ?? []),
        item.title,
      ]);
    }
  };
  for (const { story, tasks } of stories) {
    visit(story, story.key);
    tasks.forEach((task) => visit(task, story.title));
    if (story.state.category !== 'removed') {
      points[story.state.category] += story.points ?? 0;
    }
  }
  unparented.forEach((task) => visit(task, task.key));
  return { ready, activeByUser, points };
}
