import type { CodeSide, ReviewOutcomeDto, TaskChangesDto } from '@shiplean/core/api/taskChanges';
import type { ViewedState } from './viewedStore';

export type DiffLayout = 'unified' | 'split';

/** The line a new comment is for: what the diff's + button hands to the composer. */
export interface ComposerTarget {
  repo: string;
  sha: string;
  path: string;
  side: CodeSide;
  line: number;
  startLine: number | null;
  lineText: string;
}

export interface ReviewActions {
  approve(note: string | null): Promise<ReviewOutcomeDto>;
  requestChanges(note: string | null, keepAssignee: boolean): Promise<ReviewOutcomeDto>;
}

export interface ViewedProps {
  viewed: ViewedState;
  onToggleViewed(repo: string, path: string, patchId: string, viewed: boolean): void;
}

export type ChangesData = TaskChangesDto;
