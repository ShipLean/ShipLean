import type { SprintDto } from '@shiplean/core/api/sprints';
import type { SprintFigures } from '../sprintsApi';

/** What one All sprints row takes, on the desktop table and the touch list alike. */
export interface SprintRowProps {
  projectKey: string;
  sprint: SprintDto;
  figures: SprintFigures | undefined;
  /** What a sprint with no entered capacity plans against; undefined while it loads. */
  velocity: number | undefined;
  /** Opens the Edit sprint rail for this sprint; `trigger` is the button to return focus to, none from the ⋯ sheet. */
  onEdit: (trigger: HTMLButtonElement | null) => void;
  /** Opens the Close sprint dialog for this sprint (the touch row's ⋯ sheet). */
  onCloseSprint: () => void;
}
