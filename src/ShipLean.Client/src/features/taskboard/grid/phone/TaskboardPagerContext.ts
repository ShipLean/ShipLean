import { createContext, useContext } from 'react';

/** The phone grid's pager: the state on screen, and turning one page left (-1) or right (1). */
export interface TaskboardPager {
  stateId: string;
  turn(dir: -1 | 1): void;
}

export const TaskboardPagerContext = createContext<TaskboardPager | null>(null);

/** The phone grid's pager, or null outside one (the desktop grid). */
export const useTaskboardPager = (): TaskboardPager | null => useContext(TaskboardPagerContext);
