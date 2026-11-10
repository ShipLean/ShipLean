import type { TaskboardViewDto } from '@shiplean/core/api/views';

/** What the shared Taskboard grid, rows, cards and drag code read: a board view without its scope (a sprint id on the Taskboard). */
export type TaskboardGridView = Omit<TaskboardViewDto, 'sprintId'>;
