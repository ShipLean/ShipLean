import type { VelocityDto } from '../../../api/sprints';
import type { BacklogSectionDto } from '../../../api/views';

/** Where the pull line sits in the Backlog section. */
export interface PullLine {
  /** The row the line is drawn under; null for no line. */
  afterRowId: string | null;
}

export interface PullLineInput {
  section: BacklogSectionDto;
  velocity: VelocityDto | null;
  forecast: boolean;
  projectId: string;
  /** A filter, search or check queue is on: the line is off while rows are hidden. */
  filtering: boolean;
}

const none: PullLine = { afterRowId: null };

/** The forecast's cut as a line in the Backlog. */
// TODO: the cut rule, the Pull action and dragging the line replace this absent line.
export function usePullLine(input: PullLineInput): PullLine {
  void input;
  return none;
}
