import { taskChangesTestIds as ids } from './testIds';
import type { ComposerTarget } from './types';

export interface CodeCommentComposerProps {
  target: ComposerTarget;
  onPost(body: string): Promise<void>;
  onCancel(): void;
}

// Stub: the comment box replaces this.
export function CodeCommentComposer({ target }: CodeCommentComposerProps) {
  return (
    <div data-testid={ids.composer}>
      {target.path}:{target.line}
    </div>
  );
}
